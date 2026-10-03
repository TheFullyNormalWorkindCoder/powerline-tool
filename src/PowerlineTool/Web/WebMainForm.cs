using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using PowerlineTool.Core;

namespace PowerlineTool.Web;

/// <summary>
/// The default window: a WebView2 control showing the HTML interface from <c>ui/</c> (compiled into the exe).
/// All real work (scanning, history, settings, tray, files) stays in C#; the page only draws and sends commands.
/// </summary>
class WebMainForm : Form
{
    const string Origin = "https://app.powerline/";

    readonly Settings settings;
    readonly Options opts;
    readonly bool demo;
    readonly ScanCoordinator coord;
    readonly WebView2 web = new() { Dock = DockStyle.Fill, AllowExternalDrop = false };
    readonly System.Windows.Forms.Timer timer = new();
    readonly NotifyIcon tray = new();
    IDisposable singleInstance;
    Sniffer sniff;
    DateTime? nextScanAt;
    int demoTick;
    bool exiting, npcapMissing, trayHintShown;

    /// <summary>Set when WebView2 cannot start; Program then opens the classic window instead.</summary>
    public bool FallbackToClassic { get; private set; }
    public string FallbackReason { get; private set; }

    public WebMainForm(Settings settings, Options opts)
    {
        this.settings = settings; this.opts = opts; demo = opts.Demo;
        coord = new ScanCoordinator(settings, demo
            ? () => new ScanResult { Devices = DemoData.Devices(demoTick++), Nics = new List<string> { "Ethernet" } }
            : () => Scanner.ScanAllDetailed(Log));
        if (demo) DemoData.Seed(coord.History);

        Text = "Powerline Tool" + (demo ? "  (demo)" : "");
        Size = new Size(Math.Max(settings.Width, 1000), Math.Max(settings.Height, 700));
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterScreen;
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { }

        ApplyChrome();
        TopMost = settings.AlwaysOnTop;
        Controls.Add(web);

        tray.Icon = Icon ?? SystemIcons.Application; tray.Text = "Powerline Tool"; tray.Visible = !demo;
        var menu = new ContextMenuStrip();
        menu.Items.Add(L.T("Open", "Otvori"), null, (_, _) => ShowFromTray());
        menu.Items.Add(L.T("Scan now", "Skeniraj odmah"), null, (_, _) => _ = ScanNowAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("Exit", "Izlaz"), null, (_, _) => { exiting = true; Close(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowFromTray();

        timer.Tick += (_, _) => _ = ScanNowAsync();
        FormClosing += OnFormClosing;
        Load += async (_, _) => await InitWebAsync();
        HandleCreated += (_, _) => Theme.TitleBar(this);
        if (!demo) singleInstance = SingleInstance.Listen(() => { if (IsHandleCreated) BeginInvoke(ShowFromTray); });
    }

    // ---------------------------------------------------------------- window look
    void ApplyChrome()
    {
        var dark = Theme.Dark;
        BackColor = dark ? Color.FromArgb(0x09, 0x0E, 0x13) : Color.FromArgb(0xED, 0xF2, 0xF7);
        web.DefaultBackgroundColor = BackColor;
        if (IsHandleCreated) Theme.TitleBar(this);
    }

    // ---------------------------------------------------------------- WebView2 setup
    async Task InitWebAsync()
    {
        try
        {
            var dataDir = Path.Combine(Settings.Dir, "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, dataDir, new CoreWebView2EnvironmentOptions("--disable-features=msSmartScreenProtection"));
            await web.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            FallbackToClassic = true; FallbackReason = ex.Message;
            Close();
            return;
        }

        var core = web.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;   // F5 / Ctrl+R must reach the page, not reload it
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.AreDevToolsEnabled = opts.DevTools;
        core.Settings.IsWebMessageEnabled = true;

        core.AddWebResourceRequestedFilter(Origin + "*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) => ServeResource(env: web.CoreWebView2.Environment, e);
        core.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith(Origin, StringComparison.Ordinal)) e.Cancel = true; };
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.WebMessageReceived += (_, e) => OnMessage(e.WebMessageAsJson);
        core.ProcessFailed += (_, e) => Log("WebView2 process failed: " + e.ProcessFailedKind);

        core.Navigate(Origin + "index.html");
    }

    static readonly Dictionary<string, string> Mime = new()
    {
        [".html"] = "text/html; charset=utf-8", [".css"] = "text/css; charset=utf-8", [".js"] = "application/javascript; charset=utf-8",
        [".svg"] = "image/svg+xml", [".png"] = "image/png", [".ico"] = "image/x-icon", [".json"] = "application/json",
    };

    // Locked down: scripts and styles only from the embedded UI, no network access from the page at all.
    const string Csp = "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'none'; base-uri 'none'; form-action 'none'";

    void ServeResource(CoreWebView2Environment env, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var path = new Uri(e.Request.Uri).AbsolutePath.TrimStart('/');
        if (path == "") path = "index.html";
        var stream = typeof(WebMainForm).Assembly.GetManifestResourceStream("ui/" + path);
        if (stream == null) { e.Response = env.CreateWebResourceResponse(null, 404, "Not Found", ""); return; }
        var type = Mime.TryGetValue(Path.GetExtension(path).ToLowerInvariant(), out var m) ? m : "application/octet-stream";
        e.Response = env.CreateWebResourceResponse(stream, 200, "OK",
            $"Content-Type: {type}\r\nCache-Control: no-store\r\nContent-Security-Policy: {Csp}\r\nX-Content-Type-Options: nosniff");
    }

    // ---------------------------------------------------------------- talking to the page
    /// <summary>Safe from any thread: the scanner logs from worker threads, and WebView2 may only be touched on the UI thread.</summary>
    void Post(object message)
    {
        if (IsDisposed || !IsHandleCreated) return;
        var json = JsonSerializer.Serialize(message, UiState.Json);
        if (InvokeRequired)
        {
            try { BeginInvoke(() => PostJson(json)); } catch (InvalidOperationException) { /* window is closing */ }
        }
        else PostJson(json);
    }

    void PostJson(string json)
    {
        var core = web.CoreWebView2;
        if (core != null && !IsDisposed) core.PostWebMessageAsJson(json);
    }

    UiFlags Flags() => new(Updater.CurrentVersion, demo, nextScanAt, sniff != null, npcapMissing, Startup.IsEnabled());
    void PushState() => Post(UiState.Build(coord, Flags()));
    void Log(string line) => Post(new { type = "log", line = $"{DateTime.Now:HH:mm:ss}  {line}" });
    void Toast(string kind, string title, string text = null) => Post(new { type = "toast", kind, title, text });

    void OnMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            switch (root.GetProperty("cmd").GetString())
            {
                case "ready":
                    PushState();
                    if (!string.IsNullOrEmpty(opts.Page)) Post(new { type = "page", page = opts.Page });
                    _ = ScanNowAsync();
                    if (opts.Shot != null) _ = TakeShotAsync(opts.Shot);
                    break;
                case "scan": _ = ScanNowAsync(); break;
                case "settings": ApplySettings(root.GetProperty("settings")); break;
                case "rename": Rename(root.GetProperty("mac").GetString(), root.GetProperty("name").GetString()); break;
                case "clearHistory": coord.ClearHistory(); PushState(); break;
                case "copy": try { Clipboard.SetText(root.GetProperty("text").GetString() ?? ""); } catch { } break;
                case "open": { var u = root.GetProperty("url").GetString(); if (Updater.IsAllowedUrl(u)) Updater.Open(u); break; }
                case "openFolder": Directory.CreateDirectory(Settings.Dir); Process.Start("explorer.exe", Settings.Dir); break;
                case "checkUpdates": _ = CheckUpdatesAsync(); break;
                case "export": Export(root.GetProperty("format").GetString()); break;
                case "sniff": ToggleSniff(root.GetProperty("on").GetBoolean()); break;
                case "saveText": SaveText(root.GetProperty("filename").GetString(), root.GetProperty("filter").GetString(), root.GetProperty("content").GetString()); break;
                case "testNotification": TestNotification(); break;
                case "resetSettings":
                    settings.ResetPreferences();
                    L.Override = settings.Language; Theme.Apply(Theme.Resolve(settings.Theme)); ApplyChrome();
                    TopMost = settings.AlwaysOnTop; Save(); ScheduleNextIfIdle(); PushState();
                    break;
            }
        }
        catch (Exception ex) { Log("UI message failed: " + ex.Message); }
    }

    // ---------------------------------------------------------------- scanning
    async Task ScanNowAsync()
    {
        if (coord.Scanning) return;
        timer.Stop(); nextScanAt = null;
        var task = coord.ScanAsync();
        PushState();                                     // shows "scanning" right away
        try
        {
            var outcome = await task;
            if (outcome == null) return;
            npcapMissing = false;
            Announce(outcome.Changes);
        }
        catch (Exception ex) when (ex is DllNotFoundException || ex.InnerException is DllNotFoundException || ex is TypeInitializationException)
        {
            npcapMissing = true;
            Log("Npcap is missing: " + ex.Message);
        }
        catch (Exception ex)
        {
            Log("Scan failed: " + ex.Message);
            Toast("bad", L.T("Scan failed", "Skeniranje nije uspjelo"), ex.Message);
        }
        finally
        {
            ScheduleNext();
            PushState();
        }
    }

    void ScheduleNext()
    {
        timer.Stop();
        if (settings.AutoRefresh && !exiting)
        {
            timer.Interval = Math.Max(3, settings.IntervalSec) * 1000;
            nextScanAt = DateTime.Now.AddMilliseconds(timer.Interval);
            timer.Start();
        }
        else nextScanAt = null;
    }

    string Describe(NetworkChange c) => c.Kind switch
    {
        ChangeKind.DeviceAppeared => L.F("{0} came online", "{0} je online", settings.NameOf(c.Mac)),
        ChangeKind.DeviceLost => L.F("{0} went offline", "{0} je offline", settings.NameOf(c.Mac)),
        ChangeKind.LinkLow => L.F("Link {0} ↔ {1} dropped to {2} Mbps", "Veza {0} ↔ {1} pala je na {2} Mbps", settings.NameOf(c.Mac), settings.NameOf(c.Peer), c.Mbps),
        _ => L.F("Link {0} ↔ {1} recovered ({2} Mbps)", "Veza {0} ↔ {1} se oporavila ({2} Mbps)", settings.NameOf(c.Mac), settings.NameOf(c.Peer), c.Mbps),
    };

    /// <summary>In-window toasts come from the page; a tray balloon is only for when the window is out of sight.</summary>
    void Announce(List<NetworkChange> changes)
    {
        if (changes.Count == 0 || !settings.Notify || demo) return;
        if (Visible && WindowState != FormWindowState.Minimized && ContainsFocus) return;
        bool bad = changes.Any(c => c.Kind is ChangeKind.DeviceLost or ChangeKind.LinkLow);
        tray.BalloonTipTitle = "Powerline Tool";
        tray.BalloonTipText = string.Join("\n", changes.Take(4).Select(Describe));
        tray.BalloonTipIcon = bad ? ToolTipIcon.Warning : ToolTipIcon.Info;
        tray.ShowBalloonTip(6000);
    }

    // ---------------------------------------------------------------- settings & actions
    void ApplySettings(JsonElement s)
    {
        foreach (var p in s.EnumerateObject())
        {
            var v = p.Value;
            switch (p.Name)
            {
                case "autoRefresh": settings.AutoRefresh = v.GetBoolean(); break;
                case "intervalSec": settings.IntervalSec = Math.Clamp(v.GetInt32(), 3, 600); break;
                case "theme": settings.Theme = v.GetString() is "light" or "dark" ? v.GetString() : "system"; break;
                case "language": settings.Language = v.GetString() is "en" or "hr" ? v.GetString() : "auto"; break;
                case "accent": settings.Accent = v.GetString() is "blue" or "violet" or "green" or "orange" or "pink" ? v.GetString() : "teal"; break;
                case "warnBelowMbps": settings.WarnBelowMbps = Math.Clamp(v.GetInt32(), 1, 1000); break;
                case "notify": settings.Notify = v.GetBoolean(); break;
                case "minimizeToTray": settings.MinimizeToTray = v.GetBoolean(); break;
                case "logHistory": settings.LogHistory = v.GetBoolean(); break;
                case "reduceMotion": settings.ReduceMotion = v.GetBoolean(); break;
                case "alwaysOnTop": settings.AlwaysOnTop = v.GetBoolean(); TopMost = settings.AlwaysOnTop; break;
                case "ui": settings.Ui = v.GetString() == "classic" ? "classic" : "web"; break;
                case "startWithWindows":
                    try { Startup.Set(v.GetBoolean()); } catch (Exception ex) { Toast("warn", L.T("Could not change startup setting", "Nije moguće promijeniti pokretanje"), ex.Message); }
                    break;
            }
        }
        L.Override = settings.Language;
        Theme.Apply(Theme.Resolve(settings.Theme));
        ApplyChrome();
        Save();
        ScheduleNextIfIdle();
        PushState();
    }

    void ScheduleNextIfIdle() { if (!coord.Scanning) ScheduleNext(); }

    void Rename(string mac, string name)
    {
        if (string.IsNullOrWhiteSpace(mac)) return;
        name = (name ?? "").Trim();
        if (name.Length > 40) name = name[..40];
        if (name.Length == 0 || name == MacUtil.DefaultName(mac)) settings.Names.Remove(mac); else settings.Names[mac] = name;
        Save();
        PushState();
    }

    void Save()
    {
        if (demo) return;
        try
        {
            if (WindowState == FormWindowState.Normal) { settings.Width = Width; settings.Height = Height; }
            settings.Save();
        }
        catch (Exception ex) { Log("Settings: " + ex.Message); }
    }

    async Task CheckUpdatesAsync()
    {
        try
        {
            var (newer, tag, url) = await Updater.CheckAsync();
            Post(new { type = "update", newer, tag, url });
        }
        catch (Exception ex) { Post(new { type = "update", error = ex.Message }); }
    }

    void ToggleSniff(bool on)
    {
        try
        {
            if (on && sniff == null)
                sniff = new Sniffer(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "powerline-sniff.txt"), Log);
            else if (!on && sniff != null) { sniff.Dispose(); sniff = null; }
            Post(new { type = "sniff", on = sniff != null });
        }
        catch (Exception ex)
        {
            sniff = null;
            Toast("bad", L.T("Capture failed", "Snimanje nije uspjelo"), ex.Message);
            Post(new { type = "sniff", on = false });
        }
        PushState();
    }

    /// <summary>Save-as for text the page produced (diagnostics, history CSV). The file name and filter are only suggestions.</summary>
    void SaveText(string filename, string filter, string content)
    {
        var safe = Path.GetFileName(filename ?? "powerline.txt");
        using var dlg = new SaveFileDialog { Filter = string.IsNullOrWhiteSpace(filter) ? "Text|*.txt" : filter, FileName = safe };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dlg.FileName, content ?? "", new UTF8Encoding(true));
            Post(new { type = "saved", path = dlg.FileName });
        }
        catch (Exception ex) { Toast("bad", L.T("Could not save the file", "Datoteka nije spremljena"), ex.Message); }
    }

    void TestNotification()
    {
        tray.BalloonTipTitle = "Powerline Tool";
        tray.BalloonTipText = L.T("Notifications work. You will see this when an adapter disappears or a link gets slow.",
                                  "Obavijesti rade. Ovo ćeš vidjeti kad adapter nestane ili veza uspori.");
        tray.BalloonTipIcon = ToolTipIcon.Info;
        tray.Visible = true;
        tray.ShowBalloonTip(5000);
        Toast("info", L.T("Test notification sent", "Probna obavijest poslana"), L.T("Look in the corner of your screen.", "Pogledaj u kut zaslona."));
    }

    void Export(string format)
    {
        var json = format == "json";
        using var dlg = new SaveFileDialog { Filter = json ? "JSON|*.json" : "CSV|*.csv", FileName = json ? "powerline.json" : "powerline.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dlg.FileName, json ? Exporter.Json(coord.Devices, settings) : Exporter.Csv(coord.Devices, settings), new UTF8Encoding(true));
            Post(new { type = "saved", path = dlg.FileName });
        }
        catch (Exception ex) { Toast("bad", L.T("Could not save the file", "Datoteka nije spremljena"), ex.Message); }
    }

    // ---------------------------------------------------------------- screenshots for the README (--shot=file.png)
    async Task TakeShotAsync(string path)
    {
        await Task.Delay(3500);
        try
        {
            using var ms = new MemoryStream();
            await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
            File.WriteAllBytes(path, ms.ToArray());
        }
        catch (Exception ex) { Log("Shot failed: " + ex.Message); }
        exiting = true; Close();
    }

    // ---------------------------------------------------------------- window / tray
    void ShowFromTray() { Show(); WindowState = FormWindowState.Normal; Activate(); }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (opts.Minimized && !demo) { if (settings.MinimizeToTray) Hide(); else WindowState = FormWindowState.Minimized; }
    }

    void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        if (settings.MinimizeToTray && !exiting && !demo && !FallbackToClassic && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true; Hide();
            if (!trayHintShown)
            {
                trayHintShown = true;
                tray.ShowBalloonTip(3000, "Powerline Tool", L.T("Still running in the tray. Right-click the icon to exit.", "I dalje radi u traci. Desni klik na ikonu za izlaz."), ToolTipIcon.Info);
            }
            return;
        }
        timer.Stop();
        if (!FallbackToClassic) Save();
        sniff?.Dispose();
        singleInstance?.Dispose();
        tray.Visible = false; tray.Dispose();
    }
}
