using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PowerlineTool.Controls;
using PowerlineTool.Core;

namespace PowerlineTool.Forms;

enum Page { Devices, Map, History }

class MainForm : Form
{
    readonly Settings settings;
    readonly Options opts;
    readonly bool demo;
    readonly HistoryStore history = new();
    List<PlcDevice> devices = new();
    int demoTick;
    bool busy, exiting, npcapWarned, trayHintShown, refreshingCombo;
    Sniffer sniff;
    DeviceCard selected;
    Page page = Page.Devices;
    List<(string A, string B)> pairs = new();

    // header
    readonly Panel header = new() { Dock = DockStyle.Top, Height = 64 };
    readonly Label title = new() { Text = "Powerline Tool", ForeColor = Color.White, Font = new Font(Theme.Font, 16f, FontStyle.Bold), AutoSize = true, Location = new Point(20, 14) };
    readonly FlowLayoutPanel actions = new() { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 16, 14, 0) };
    readonly CheckBox auto = new() { Text = L.T("Auto-refresh every", "Auto-osvježi svakih"), AutoSize = true, ForeColor = Color.White, Margin = new Padding(0, 6, 4, 0) };
    readonly NumericUpDown secs = new() { Minimum = 3, Maximum = 600, Value = 10, Width = 52, Margin = new Padding(0, 3, 4, 0) };
    readonly Label secLbl = new() { Text = "s", ForeColor = Color.White, AutoSize = true, Margin = new Padding(0, 7, 14, 0) };
    readonly Button scanBtn = HeaderButton(L.T("Scan", "Skeniraj"), true);
    readonly Button menuBtn = HeaderButton("⋯", false);

    // nav + summary
    readonly Panel subbar = new() { Dock = DockStyle.Top, Height = 42 };
    readonly NavButton navDevices = new() { Text = L.T("Devices", "Uređaji"), Location = new Point(14, 0), Active = true };
    readonly NavButton navMap = new() { Text = L.T("Map", "Mapa"), Location = new Point(124, 0), Width = 90 };
    readonly NavButton navHistory = new() { Text = L.T("History", "Povijest"), Location = new Point(214, 0), Width = 100 };
    readonly Label summary = new() { Dock = DockStyle.Right, Width = 640, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 18, 0), Font = new Font(Theme.Font, 9f) };

    // pages
    readonly Panel content = new() { Dock = DockStyle.Fill };
    readonly FlowLayoutPanel body = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(20, 16, 20, 8) };
    readonly Label empty = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = new Font(Theme.Font, 11f), Height = 120, Text = L.T("Scanning...", "Skeniranje...") };
    readonly MapView map = new() { Dock = DockStyle.Fill, Visible = false };
    readonly Panel historyPage = new() { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(20, 14, 20, 14) };
    readonly Panel historyBar = new() { Dock = DockStyle.Top, Height = 44 };
    readonly Label linkLbl = new() { Text = L.T("Link:", "Veza:"), AutoSize = true, Location = new Point(0, 9) };
    readonly ComboBox linkCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(48, 5), Width = 300 };
    readonly Label stats = new() { AutoSize = false, Location = new Point(362, 0), Height = 34, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Theme.Font, 9f), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
    readonly LinkLabel clearHistory = new() { Text = L.T("Clear history", "Očisti povijest"), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
    readonly ChartView chart = new() { Dock = DockStyle.Fill };

    // footer
    readonly Panel footer = new() { Dock = DockStyle.Bottom, Height = 34 };
    readonly Label status = new() { AutoSize = true, Font = new Font(Theme.Font, 9f), Location = new Point(20, 8) };
    readonly FlowLayoutPanel footRight = new() { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 4, 12, 0) };
    readonly LinkLabel logToggle = new() { Text = L.T("Show log", "Prikaži log"), AutoSize = true, Margin = new Padding(0, 4, 14, 0) };
    readonly LinkLabel sniffLink = new() { Text = L.T("Capture traffic (diagnostics)", "Snimaj promet (dijagnostika)"), AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
    readonly TextBox log = new() { Dock = DockStyle.Bottom, Height = 150, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5f), Visible = false };

    readonly System.Windows.Forms.Timer timer = new();
    readonly ContextMenuStrip cardMenu = new();
    readonly ContextMenuStrip mainMenu = new();
    readonly ToolTip tip = new() { InitialDelay = 400, AutoPopDelay = 12000 };
    readonly NotifyIcon tray = new();
    EventWaitHandle showEvent;

    static Button HeaderButton(string text, bool primary) => new()
    {
        Text = text, AutoSize = true, MinimumSize = new Size(primary ? 96 : 40, 32), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
        Font = new Font(Theme.Font, primary ? 9.5f : 12f, FontStyle.Bold), Margin = new Padding(0, 0, 8, 0),
        BackColor = primary ? Color.White : Theme.Teal, ForeColor = primary ? Theme.TealDark : Color.White,
        FlatAppearance = { BorderColor = Color.White, BorderSize = 1 },
    };

    public MainForm(Settings settings, Options opts)
    {
        this.settings = settings; this.opts = opts; demo = opts.Demo;
        Text = "Powerline Tool" + (demo ? "  (demo)" : "");
        Size = new Size(settings.Width, settings.Height); MinimumSize = new Size(760, 460);
        StartPosition = FormStartPosition.CenterScreen; Font = new Font(Theme.Font, 9.5f); KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { }

        BuildLayout();
        BuildMenus();
        WireEvents();

        auto.Checked = !demo && settings.AutoRefresh; secs.Value = settings.IntervalSec;
        timer.Interval = settings.IntervalSec * 1000; timer.Enabled = auto.Checked;
        linkCombo.SelectedIndexChanged += (_, _) => { if (!refreshingCombo) UpdateChart(); };

        if (demo) DemoData.Seed(history);
        else
        {
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "PowerlineTool.Show");
            ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => { if (IsHandleCreated) BeginInvoke(ShowFromTray); }, null, -1, false);
        }

        tray.Icon = Icon ?? SystemIcons.Application; tray.Text = "Powerline Tool"; tray.Visible = !demo;
        ApplyTheme();
        ShowPage(opts.Page?.ToLowerInvariant() switch { "map" => Page.Map, "history" => Page.History, _ => Page.Devices });
    }

    // ---- layout ------------------------------------------------------------------------

    void BuildLayout()
    {
        actions.Controls.AddRange(new Control[] { auto, secs, secLbl, scanBtn, menuBtn });
        header.Controls.Add(actions); header.Controls.Add(title);

        subbar.Controls.Add(summary);
        subbar.Controls.AddRange(new Control[] { navDevices, navMap, navHistory });
        subbar.Paint += (_, e) => { using var p = new Pen(Theme.Border); e.Graphics.DrawLine(p, 0, subbar.Height - 1, subbar.Width, subbar.Height - 1); };

        body.Controls.Add(empty);

        historyBar.Controls.AddRange(new Control[] { linkLbl, linkCombo, stats, clearHistory });
        historyPage.Controls.Add(chart); historyPage.Controls.Add(historyBar);
        historyBar.Resize += (_, _) =>
        {
            clearHistory.Location = new Point(historyBar.Width - clearHistory.Width, 9);
            stats.Width = Math.Max(100, clearHistory.Left - stats.Left - 10);
        };

        content.Controls.Add(body); content.Controls.Add(map); content.Controls.Add(historyPage);

        footRight.Controls.AddRange(new Control[] { logToggle, sniffLink });
        footer.Controls.Add(footRight); footer.Controls.Add(status);
        footer.Paint += (_, e) => { using var p = new Pen(Theme.Border); e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };

        Controls.Add(content); Controls.Add(log); Controls.Add(footer); Controls.Add(subbar); Controls.Add(header);
    }

    void BuildMenus()
    {
        cardMenu.Items.Add(L.T("Rename…", "Preimenuj…"), null, (_, _) => RenameSelected());
        cardMenu.Items.Add(L.T("Copy MAC", "Kopiraj MAC"), null, (_, _) => { if (selected?.Dev != null) Clipboard.SetText(selected.Dev.Mac); });
        cardMenu.Items.Add(L.T("Show history", "Prikaži povijest"), null, (_, _) => ShowHistoryFor(selected?.Dev?.Mac));

        mainMenu.Items.Add(L.T("Settings…", "Postavke…") + "\tCtrl+,", null, (_, _) => OpenSettings());
        mainMenu.Items.Add(new ToolStripSeparator());
        mainMenu.Items.Add(L.T("Export CSV…", "Izvezi CSV…") + "\tCtrl+E", null, (_, _) => ExportCsv());
        mainMenu.Items.Add(L.T("Export JSON…", "Izvezi JSON…"), null, (_, _) => ExportJson());
        mainMenu.Items.Add(L.T("Copy summary", "Kopiraj sažetak"), null, (_, _) => CopySummary());
        mainMenu.Items.Add(new ToolStripSeparator());
        mainMenu.Items.Add(L.T("Open data folder", "Otvori mapu s podacima"), null, (_, _) => { Directory.CreateDirectory(Settings.Dir); Process.Start("explorer.exe", Settings.Dir); });
        mainMenu.Items.Add(L.T("Check for updates", "Provjeri ažuriranja"), null, async (_, _) => await CheckUpdatesAsync());
        mainMenu.Items.Add(L.T("About…", "O programu…") + "\tF1", null, (_, _) => ShowAbout());
        mainMenu.Items.Add(new ToolStripSeparator());
        mainMenu.Items.Add(L.T("Exit", "Izlaz"), null, (_, _) => ExitApp());

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add(L.T("Open", "Otvori"), null, (_, _) => ShowFromTray());
        trayMenu.Items.Add(L.T("Scan now", "Skeniraj odmah"), null, (_, _) => _ = ScanAsync());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(L.T("Exit", "Izlaz"), null, (_, _) => ExitApp());
        tray.ContextMenuStrip = trayMenu;
    }

    void WireEvents()
    {
        scanBtn.Click += (_, _) => _ = ScanAsync();
        menuBtn.Click += (_, _) => mainMenu.Show(menuBtn, new Point(0, menuBtn.Height));
        navDevices.Click += (_, _) => ShowPage(Page.Devices);
        navMap.Click += (_, _) => ShowPage(Page.Map);
        navHistory.Click += (_, _) => ShowPage(Page.History);
        auto.CheckedChanged += (_, _) => timer.Enabled = auto.Checked;
        secs.ValueChanged += (_, _) => timer.Interval = (int)secs.Value * 1000;
        timer.Tick += (_, _) => _ = ScanAsync();
        body.Resize += (_, _) => FitCards();
        logToggle.LinkClicked += (_, _) => SetLogVisible(!log.Visible);
        sniffLink.LinkClicked += (_, _) => ToggleSniff();
        clearHistory.LinkClicked += (_, _) => { history.Clear(); RefreshHistory(); };
        tray.DoubleClick += (_, _) => ShowFromTray();
        Shown += (_, _) =>
        {
            if (opts.Minimized && !demo) { if (settings.MinimizeToTray) Hide(); else WindowState = FormWindowState.Minimized; }
            _ = ScanAsync();
        };
        HandleCreated += (_, _) => Theme.TitleBar(this);
        FormClosing += OnFormClosing;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F5) { _ = ScanAsync(); e.Handled = true; }
            else if (e.KeyCode == Keys.F1) { ShowAbout(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.E) { ExportCsv(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Oemcomma) { OpenSettings(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.L) { SetLogVisible(!log.Visible); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.D1) ShowPage(Page.Devices);
            else if (e.Control && e.KeyCode == Keys.D2) ShowPage(Page.Map);
            else if (e.Control && e.KeyCode == Keys.D3) ShowPage(Page.History);
        };
    }

    // ---- theme -------------------------------------------------------------------------

    void ApplyTheme()
    {
        BackColor = Theme.Bg; ForeColor = Theme.Text;
        header.BackColor = Theme.Header; subbar.BackColor = Theme.Card; footer.BackColor = Theme.Card;
        content.BackColor = Theme.Bg; body.BackColor = Theme.Bg; historyPage.BackColor = Theme.Bg; historyBar.BackColor = Theme.Bg;
        summary.ForeColor = Theme.Text; summary.BackColor = Theme.Card; status.ForeColor = Theme.Muted;
        empty.ForeColor = Theme.Muted; empty.BackColor = Theme.Bg;
        linkLbl.ForeColor = Theme.Text; stats.ForeColor = Theme.Muted;
        log.BackColor = Theme.Card; log.ForeColor = Theme.Text;
        foreach (var c in new Control[] { linkCombo, secs }) { c.BackColor = Theme.InputBg; c.ForeColor = Theme.Text; }
        secs.BackColor = Theme.InputBg;
        foreach (var l in new[] { logToggle, sniffLink, clearHistory }) { l.LinkColor = Theme.TealText; l.ActiveLinkColor = Theme.Teal; l.BackColor = l == clearHistory ? Theme.Bg : Theme.Card; }
        menuBtn.BackColor = Theme.Teal;
        foreach (var m in new ToolStrip[] { cardMenu, mainMenu, tray.ContextMenuStrip })
        {
            if (m == null) continue;
            m.BackColor = Theme.Card; m.ForeColor = Theme.Text;
            foreach (ToolStripItem i in m.Items) { i.BackColor = Theme.Card; i.ForeColor = Theme.Text; }
        }
        Theme.TitleBar(this);
        foreach (Control c in new Control[] { navDevices, navMap, navHistory, map, chart, subbar, footer }) c.Invalidate();
        foreach (var c in body.Controls.OfType<DeviceCard>()) c.Invalidate();
    }

    // ---- pages -------------------------------------------------------------------------

    void ShowPage(Page p)
    {
        page = p;
        body.Visible = p == Page.Devices; map.Visible = p == Page.Map; historyPage.Visible = p == Page.History;
        navDevices.Active = p == Page.Devices; navMap.Active = p == Page.Map; navHistory.Active = p == Page.History;
        if (p == Page.History) RefreshHistory();
        if (p == Page.Map) { map.NameOf = settings.NameOf; map.Devices = devices; map.Invalidate(); }
    }

    void ShowHistoryFor(string mac)
    {
        if (mac == null) return;
        ShowPage(Page.History);
        int i = pairs.FindIndex(p => p.A == mac || p.B == mac);
        if (i >= 0) linkCombo.SelectedIndex = i;
    }

    void RefreshHistory()
    {
        var keep = linkCombo.SelectedIndex >= 0 && linkCombo.SelectedIndex < pairs.Count ? pairs[linkCombo.SelectedIndex] : default;
        pairs = history.Pairs.ToList();
        refreshingCombo = true;
        linkCombo.Items.Clear();
        foreach (var (a, b) in pairs) linkCombo.Items.Add($"{settings.NameOf(a)}  ↔  {settings.NameOf(b)}");
        int idx = pairs.FindIndex(p => p == keep);
        if (idx < 0 && pairs.Count > 0) idx = 0;
        if (idx >= 0) linkCombo.SelectedIndex = idx;
        refreshingCombo = false;
        UpdateChart();
    }

    void UpdateChart()
    {
        chart.WarnLine = settings.WarnBelowMbps;
        int i = linkCombo.SelectedIndex;
        if (i < 0 || i >= pairs.Count) { chart.Data = Array.Empty<Sample>(); stats.Text = ""; chart.Invalidate(); return; }
        var s = history.Series(pairs[i].A, pairs[i].B);
        chart.Data = s;
        if (s.Count > 0)
        {
            string Line(string n, Func<Sample, int> f) => $"{n}  min {s.Min(f)} · avg {s.Average(f):0} · max {s.Max(f)}";
            stats.Text = $"{Line("TX", x => x.Tx)}    {Line("RX", x => x.Rx)}    ({s.Count} " + L.T("samples", "uzoraka") + ")";
        }
        chart.Invalidate();
    }

    void Render()
    {
        var keep = selected?.Dev?.Mac;
        selected = null;
        body.SuspendLayout();
        foreach (var c in body.Controls.OfType<DeviceCard>().ToList()) { body.Controls.Remove(c); c.Dispose(); }
        body.Controls.Remove(empty);

        if (devices.Count == 0)
        {
            empty.Text = L.T("No powerline adapter found.\r\nCheck the cable and press Scan.", "Nije pronađen nijedan powerline adapter.\r\nProvjeri kabel i klikni Skeniraj.");
            body.Controls.Add(empty);
        }
        foreach (var d in devices.OrderByDescending(d => d.IsLocal).ThenBy(d => d.Mac))
        {
            var card = new DeviceCard { ContextMenuStrip = cardMenu };
            card.Bind(d, settings.NameOf);
            tip.SetToolTip(card, card.Details());
            card.MouseDown += (_, _) => Select(card);
            body.Controls.Add(card);
            if (d.Mac == keep) Select(card);
        }
        FitCards();
        body.ResumeLayout();

        map.NameOf = settings.NameOf; map.Devices = devices; map.Invalidate();
        UpdateSummary();
    }

    void UpdateSummary()
    {
        if (devices.Count == 0) { summary.Text = ""; return; }
        var s = Summary.Of(devices);
        summary.Text = s.Links == 0
            ? L.F("{0} device(s) · no link data", "{0} uređaja · nema podataka o vezi", s.Devices)
            : L.F("{0} devices · {1} link(s) · avg {2:0} Mbps · weakest {5} Mbps ({3} ↔ {4})",
                  "{0} uređaja · {1} veza · prosjek {2:0} Mbps · najslabija {5} Mbps ({3} ↔ {4})",
                  s.Devices, s.Links, s.AvgMbps, settings.NameOf(s.WeakestFrom), settings.NameOf(s.WeakestTo), s.WeakestMbps);
    }

    void Select(DeviceCard c)
    {
        if (selected != null) { selected.Selected = false; selected.Invalidate(); }
        selected = c; c.Selected = true; c.Invalidate();
    }

    void FitCards()
    {
        int w = Math.Max(300, body.ClientSize.Width - body.Padding.Horizontal);
        foreach (Control c in body.Controls) c.Width = w;
    }

    // ---- scanning ----------------------------------------------------------------------

    void Log(string s)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(() => log.AppendText($"{DateTime.Now:HH:mm:ss}  {s}\r\n")); } catch { }
    }

    async Task ScanAsync()
    {
        if (busy) return;
        busy = true; scanBtn.Enabled = false; scanBtn.Text = L.T("Scanning…", "Skeniram…"); status.Text = L.T("Scanning…", "Skeniranje…");
        try
        {
            var result = demo ? DemoData.Devices(demoTick++) : await Task.Run(() => Scanner.ScanAll(Log));
            var changes = ChangeDetector.Diff(devices, result, settings.WarnBelowMbps);
            devices = result;
            var now = DateTime.Now;
            history.Add(now, devices);
            if (settings.LogHistory && !demo)
                try { HistoryCsv.Append(Settings.HistoryPath, now, devices); } catch (Exception ex) { Log("History file: " + ex.Message); }

            Render();
            if (page == Page.History) RefreshHistory();
            Announce(changes);
            status.Text = devices.Count == 0
                ? L.T("No powerline adapter found.", "Nije pronađen nijedan powerline adapter.")
                : L.F("Devices: {0}   ·   last scan {1:HH:mm:ss}", "Uređaja: {0}   ·   zadnje skeniranje {1:HH:mm:ss}", devices.Count, now);
        }
        catch (Exception ex) when (ex is DllNotFoundException || ex.InnerException is DllNotFoundException || ex is TypeInitializationException)
        {
            status.Text = L.T("Npcap is missing", "Npcap nedostaje");
            if (!npcapWarned)
            {
                npcapWarned = true;
                MessageBox.Show(this, L.T("Npcap is not installed. Get it from https://npcap.com (tick 'WinPcap API-compatible mode').",
                                          "Npcap nije instaliran. Instaliraj ga s https://npcap.com (označi 'WinPcap API-compatible mode')."), "Powerline Tool");
            }
        }
        catch (Exception ex)
        {
            status.Text = L.T("Error", "Greška");
            Log("Scan failed: " + ex.Message);
        }
        finally { busy = false; scanBtn.Enabled = true; scanBtn.Text = L.T("Scan", "Skeniraj"); }
    }

    string Describe(NetworkChange c) => c.Kind switch
    {
        ChangeKind.DeviceAppeared => L.F("{0} came online", "{0} je online", settings.NameOf(c.Mac)),
        ChangeKind.DeviceLost => L.F("{0} went offline", "{0} je offline", settings.NameOf(c.Mac)),
        ChangeKind.LinkLow => L.F("Link {0} ↔ {1} dropped to {2} Mbps", "Veza {0} ↔ {1} pala je na {2} Mbps", settings.NameOf(c.Mac), settings.NameOf(c.Peer), c.Mbps),
        _ => L.F("Link {0} ↔ {1} recovered ({2} Mbps)", "Veza {0} ↔ {1} se oporavila ({2} Mbps)", settings.NameOf(c.Mac), settings.NameOf(c.Peer), c.Mbps),
    };

    void Announce(List<NetworkChange> changes)
    {
        if (changes.Count == 0) return;
        var lines = changes.Select(Describe).ToList();
        foreach (var l in lines) Log(l);
        if (!settings.Notify || demo) return;
        bool bad = changes.Any(c => c.Kind is ChangeKind.DeviceLost or ChangeKind.LinkLow);
        tray.BalloonTipTitle = "Powerline Tool";
        tray.BalloonTipText = string.Join("\n", lines.Take(4));
        tray.BalloonTipIcon = bad ? ToolTipIcon.Warning : ToolTipIcon.Info;
        tray.ShowBalloonTip(6000);
    }

    // ---- actions -----------------------------------------------------------------------

    void RenameSelected()
    {
        if (selected?.Dev == null) return;
        var mac = selected.Dev.Mac;
        var name = Dialogs.Prompt(this, L.T("Rename device", "Preimenuj uređaj"), mac, settings.NameOf(mac));
        if (name == null) return;
        settings.Names[mac] = name;
        SaveSettings();
        Render();
    }

    void OpenSettings()
    {
        using var dlg = new SettingsForm(settings);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        dlg.Apply();
        SaveSettings();
        Theme.Apply(Theme.Resolve(settings.Theme));
        ApplyTheme();
        Render();
        if (page == Page.History) RefreshHistory();
    }

    void ShowAbout() { using var a = new AboutForm(Icon ?? SystemIcons.Application); a.ShowDialog(this); }

    async Task CheckUpdatesAsync()
    {
        try
        {
            var (newer, tag, url) = await Updater.CheckAsync();
            if (!newer)
            {
                MessageBox.Show(this, L.F("You are up to date (version {0}).", "Imaš najnoviju verziju ({0}).", Updater.CurrentVersion), "Powerline Tool");
                return;
            }
            if (MessageBox.Show(this, L.F("Version {0} is available. Open the download page?", "Dostupna je verzija {0}. Otvoriti stranicu za preuzimanje?", tag),
                                "Powerline Tool", MessageBoxButtons.YesNo) == DialogResult.Yes) Updater.Open(url);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, L.T("Could not check for updates: ", "Provjera nije uspjela: ") + ex.Message, "Powerline Tool");
        }
    }

    void SetLogVisible(bool v) { log.Visible = v; logToggle.Text = v ? L.T("Hide log", "Sakrij log") : L.T("Show log", "Prikaži log"); }

    void ToggleSniff()
    {
        if (sniff != null)
        {
            sniff.Dispose(); sniff = null;
            sniffLink.Text = L.T("Capture traffic (diagnostics)", "Snimaj promet (dijagnostika)");
            Log(L.T("Capture stopped.", "Snimanje zaustavljeno."));
            return;
        }
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "powerline-sniff.txt");
        try
        {
            sniff = new Sniffer(path, Log);
            sniffLink.Text = L.T("Stop capture", "Zaustavi snimanje");
            SetLogVisible(true);
            Log(L.T("Capturing to: ", "Snimanje u: ") + path);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Powerline Tool"); }
    }

    // ---- export ------------------------------------------------------------------------

    void ExportCsv()
    {
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "powerline.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var sb = new StringBuilder("Type,Name,MAC,Link to,TX,RX,Quality,Firmware\r\n");
        foreach (var d in devices)
        {
            var type = d.IsLocal ? "Local" : "Remote";
            if (d.Links.Count == 0) sb.AppendLine($"{type},\"{settings.NameOf(d.Mac)}\",{d.Mac},,,,,\"{d.Firmware}\"");
            foreach (var l in d.Links)
                sb.AppendLine($"{type},\"{settings.NameOf(d.Mac)}\",{d.Mac},{l.Peer},{l.Tx},{l.Rx},{Quality.Of(l.Min)},\"{d.Firmware}\"");
        }
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
    }

    void ExportJson()
    {
        using var dlg = new SaveFileDialog { Filter = "JSON|*.json", FileName = "powerline.json" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var s = Summary.Of(devices);
        var doc = new
        {
            generated = DateTime.Now,
            summary = new { s.Devices, s.Links, avgMbps = Math.Round(s.AvgMbps, 1), weakestMbps = s.WeakestMbps },
            devices = devices.Select(d => new
            {
                name = settings.NameOf(d.Mac), mac = d.Mac, local = d.IsLocal, firmware = d.Firmware, nid = d.Nid, nic = d.Nic,
                links = d.Links.Select(l => new { peer = l.Peer, peerName = settings.NameOf(l.Peer), tx = l.Tx, rx = l.Rx, quality = Quality.Of(l.Min).ToString() }),
            }),
        };
        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
    }

    void CopySummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine(summary.Text);
        foreach (var d in devices)
        {
            sb.AppendLine($"{settings.NameOf(d.Mac)} ({d.Mac}){(d.IsLocal ? " [local]" : "")}");
            foreach (var l in d.Links) sb.AppendLine($"  -> {settings.NameOf(l.Peer)}: TX {l.Tx} / RX {l.Rx} Mbps ({Quality.Of(l.Min)})");
        }
        if (sb.Length > 0) Clipboard.SetText(sb.ToString());
    }

    // ---- window / tray -----------------------------------------------------------------

    void SaveSettings()
    {
        if (demo) return;
        try
        {
            if (WindowState == FormWindowState.Normal) { settings.Width = Width; settings.Height = Height; }
            settings.AutoRefresh = auto.Checked; settings.IntervalSec = (int)secs.Value;
            settings.Save();
        }
        catch (Exception ex) { Log("Settings: " + ex.Message); }
    }

    void ShowFromTray()
    {
        Show(); WindowState = FormWindowState.Normal; Activate();
    }

    void ExitApp() { exiting = true; Close(); }

    void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        if (settings.MinimizeToTray && !exiting && !demo && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true; Hide();
            if (!trayHintShown)
            {
                trayHintShown = true;
                tray.ShowBalloonTip(3000, "Powerline Tool", L.T("Still running in the tray. Right-click the icon to exit.", "I dalje radi u traci. Desni klik na ikonu za izlaz."), ToolTipIcon.Info);
            }
            return;
        }
        SaveSettings();
        sniff?.Dispose();
        timer.Stop();
        tray.Visible = false; tray.Dispose();
        showEvent?.Dispose();
    }
}
