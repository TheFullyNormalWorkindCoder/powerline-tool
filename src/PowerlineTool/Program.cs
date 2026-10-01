using System.Drawing.Drawing2D;
using System.Text;
using System.Text.Json;
using PowerlineTool;

ApplicationConfiguration.Initialize();
Application.Run(new MainForm());

namespace PowerlineTool
{
    /// <summary>Tiny localisation helper: Croatian when Windows UI language is Croatian, English otherwise.</summary>
    static class L
    {
        // Override with `--lang=en` or `--lang=hr`.
        static readonly bool Hr = Environment.GetCommandLineArgs().Contains("--lang=hr") ||
            (!Environment.GetCommandLineArgs().Contains("--lang=en") &&
             System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "hr");
        public static string T(string en, string hr) => Hr ? hr : en;
    }

    static class Theme
    {
        public static readonly Color Teal = Color.FromArgb(0x1B, 0xA9, 0xB8);
        public static readonly Color TealDark = Color.FromArgb(0x0E, 0x7C, 0x8A);
        public static readonly Color Bg = Color.FromArgb(0xF2, 0xF5, 0xF7);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(0xDD, 0xE3, 0xE8);
        public static readonly Color Text = Color.FromArgb(0x22, 0x2B, 0x33);
        public static readonly Color Muted = Color.FromArgb(0x7A, 0x88, 0x94);
        public static readonly Color Good = Color.FromArgb(0x2E, 0xB8, 0x72);
        public static readonly Color Warn = Color.FromArgb(0xF2, 0xA9, 0x1F);
        public static readonly Color Bad = Color.FromArgb(0xE5, 0x4B, 0x4B);
        public static readonly Color Track = Color.FromArgb(0xE9, 0xEE, 0xF1);
        public const string Font = "Segoe UI";
    }

    class DeviceCard : Control
    {
        const int HeaderH = 76, RowH = 34;
        public PlcDevice Dev;
        public string Title = "";
        public Func<string, string> NameOf = m => m;
        public bool Selected;

        public DeviceCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Margin = new Padding(0, 0, 0, 12);
        }

        public void Bind(PlcDevice d, string title, Func<string, string> nameOf)
        {
            Dev = d; Title = title; NameOf = nameOf;
            Height = HeaderH + Math.Max(1, d.LinkList.Count) * RowH + 10;
            Invalidate();
        }

        static GraphicsPath Round(Rectangle r, int rad)
        {
            var p = new GraphicsPath(); int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }

        static Color RateColor(int v) => v >= 100 ? Theme.Good : v >= 50 ? Theme.Warn : Theme.Bad;

        void Bar(Graphics g, int x, int y, int w, int h, int value)
        {
            using var track = Round(new Rectangle(x, y, w, h), h / 2);
            using var tb = new SolidBrush(Theme.Track);
            g.FillPath(tb, track);
            int fw = (int)(w * Math.Min(1.0, value / 600.0));
            if (fw < h) fw = Math.Min(w, h);
            using var fill = Round(new Rectangle(x, y, fw, h), h / 2);
            using var fb = new SolidBrush(RateColor(value));
            g.FillPath(fb, fill);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Bg);
            if (Dev == null) return;

            using (var path = Round(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (var fill = new SolidBrush(Theme.Card))
            using (var pen = new Pen(Selected ? Theme.Teal : Theme.Border, Selected ? 2 : 1))
            { g.FillPath(fill, path); g.DrawPath(pen, path); }

            // icon
            using (var ib = new SolidBrush(Dev.IsLocal ? Theme.Teal : Theme.TealDark))
                g.FillEllipse(ib, 18, 16, 42, 42);
            using (var f = new Font(Theme.Font, 9f, FontStyle.Bold))
                TextRenderer.DrawText(g, "PLC", f, new Rectangle(18, 16, 42, 42), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using (var f = new Font(Theme.Font, 12f, FontStyle.Bold))
                TextRenderer.DrawText(g, Title, f, new Point(72, 14), Theme.Text);
            using (var f = new Font("Consolas", 9f))
                TextRenderer.DrawText(g, Dev.Mac, f, new Point(74, 40), Theme.Muted);

            // badge
            var badge = Dev.IsLocal ? L.T("LOCAL", "LOKALNI") : L.T("REMOTE", "UDALJENI");
            using (var f = new Font(Theme.Font, 8f, FontStyle.Bold))
            {
                var sz = TextRenderer.MeasureText(badge, f);
                var br = new Rectangle(Width - 20 - sz.Width - 14, 16, sz.Width + 14, 22);
                using var bp = Round(br, 11);
                using var bb = new SolidBrush(Dev.IsLocal ? Color.FromArgb(0xD9, 0xF3, 0xF5) : Color.FromArgb(0xEC, 0xEF, 0xF2));
                g.FillPath(bb, bp);
                TextRenderer.DrawText(g, badge, f, br, Dev.IsLocal ? Theme.TealDark : Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            using (var f = new Font(Theme.Font, 8.5f))
            {
                var fw = string.IsNullOrEmpty(Dev.Firmware) ? "" : "FW " + Dev.Firmware;
                var sz = TextRenderer.MeasureText(fw, f);
                TextRenderer.DrawText(g, fw, f, new Point(Width - 20 - sz.Width, 42), Theme.Muted);
            }

            using (var sep = new Pen(Theme.Border)) g.DrawLine(sep, 18, HeaderH - 2, Width - 18, HeaderH - 2);

            if (Dev.LinkList.Count == 0)
            {
                using var f = new Font(Theme.Font, 9f, FontStyle.Italic);
                TextRenderer.DrawText(g, L.T("No link data", "Nema podataka o vezi"), f, new Point(20, HeaderH + 8), Theme.Muted);
                return;
            }

            int x0 = 230, right = Width - 20;
            int half = (right - x0) / 2;
            for (int i = 0; i < Dev.LinkList.Count; i++)
            {
                var (peer, tx, rx) = Dev.LinkList[i];
                int y = HeaderH + i * RowH;
                using var lf = new Font(Theme.Font, 9.5f);
                TextRenderer.DrawText(g, "→  " + NameOf(peer), lf, new Point(20, y + 6), Theme.Text);

                for (int k = 0; k < 2; k++)
                {
                    int bx = x0 + k * half, val = k == 0 ? tx : rx;
                    using var cf = new Font(Theme.Font, 8f, FontStyle.Bold);
                    TextRenderer.DrawText(g, k == 0 ? "TX" : "RX", cf, new Point(bx, y + 9), Theme.Muted);
                    int barW = Math.Max(50, half - 36 - 90);
                    Bar(g, bx + 26, y + 11, barW, 10, val);
                    using var vf = new Font(Theme.Font, 9.5f, FontStyle.Bold);
                    TextRenderer.DrawText(g, $"{val} Mbps", vf, new Point(bx + 26 + barW + 8, y + 5), RateColor(val));
                }
            }
        }
    }

    public class MainForm : Form
    {
        readonly Panel header = new() { Dock = DockStyle.Top, Height = 64, BackColor = Theme.TealDark };
        readonly Label title = new() { Text = "Powerline Tool", ForeColor = Color.White, Font = new Font(Theme.Font, 16f, FontStyle.Bold), AutoSize = true, Location = new Point(20, 14) };
        readonly FlowLayoutPanel actions = new() { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 16, 14, 0), BackColor = Color.Transparent };
        readonly Button scanBtn = MakeButton(L.T("Scan", "Skeniraj"), true);
        readonly Button exportBtn = MakeButton(L.T("Export CSV", "Izvezi CSV"), false);
        readonly CheckBox auto = new() { Text = L.T("Auto-refresh every", "Auto-osvježi svakih"), AutoSize = true, ForeColor = Color.White, Font = new Font(Theme.Font, 9.5f), Margin = new Padding(0, 6, 4, 0) };
        readonly NumericUpDown secs = new() { Minimum = 3, Maximum = 600, Value = 10, Width = 52, Margin = new Padding(0, 3, 4, 0) };
        readonly Label secLbl = new() { Text = "s", ForeColor = Color.White, AutoSize = true, Margin = new Padding(0, 7, 14, 0) };
        readonly FlowLayoutPanel body = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(20, 16, 20, 8), BackColor = Theme.Bg };
        readonly Label empty = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted, Font = new Font(Theme.Font, 11f), Height = 120, Text = L.T("Scanning...", "Skeniranje...") };
        readonly Panel footer = new() { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.White };
        readonly Label status = new() { AutoSize = true, ForeColor = Theme.Muted, Font = new Font(Theme.Font, 9f), Location = new Point(20, 8) };
        readonly FlowLayoutPanel footRight = new() { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 4, 12, 0) };
        readonly LinkLabel logToggle = new() { Text = L.T("Show log", "Prikaži log"), AutoSize = true, Margin = new Padding(0, 4, 14, 0), LinkColor = Theme.TealDark };
        readonly LinkLabel sniffLink = new() { Text = L.T("Capture traffic (diagnostics)", "Snimaj promet (dijagnostika)"), AutoSize = true, Margin = new Padding(0, 4, 0, 0), LinkColor = Theme.TealDark };
        readonly TextBox log = new() { Dock = DockStyle.Bottom, Height = 150, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5f), Visible = false, BackColor = Color.White };
        readonly System.Windows.Forms.Timer timer = new();
        readonly ContextMenuStrip menu = new();
        readonly Dictionary<string, string> names = new();
        readonly string namesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PowerlineTool", "names.json");
        List<PlcDevice> devices = new();
        DeviceCard selected;
        IDisposable sniff;
        volatile bool busy;
        // `--demo` shows fake devices: handy for screenshots and for trying the UI without hardware.
        readonly bool demo = Environment.GetCommandLineArgs().Skip(1).Contains("--demo");

        static List<PlcDevice> DemoDevices() => new()
        {
            new PlcDevice
            {
                Mac = "AA:BB:CC:00:00:01", IsLocal = true, Firmware = "tpver_demo_1.0", Nic = "Ethernet",
                LinkList = { ("AA:BB:CC:00:00:02", 223, 145), ("AA:BB:CC:00:00:03", 48, 61) }
            },
            new PlcDevice
            {
                Mac = "AA:BB:CC:00:00:02", Firmware = "tpver_demo_1.0", Nic = "Ethernet",
                LinkList = { ("AA:BB:CC:00:00:01", 145, 223) }
            },
            new PlcDevice
            {
                Mac = "AA:BB:CC:00:00:03", Firmware = "tpver_demo_1.0", Nic = "Ethernet",
                LinkList = { ("AA:BB:CC:00:00:01", 61, 48) }
            },
        };

        static Button MakeButton(string text, bool primary) => new()
        {
            Text = text, AutoSize = true, MinimumSize = new Size(96, 32), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font(Theme.Font, 9.5f, FontStyle.Bold), Margin = new Padding(0, 0, 8, 0),
            BackColor = primary ? Color.White : Theme.Teal, ForeColor = primary ? Theme.TealDark : Color.White,
            FlatAppearance = { BorderColor = Color.White, BorderSize = 1 }
        };

        public MainForm()
        {
            Text = "Powerline Tool"; Width = 1000; Height = 620; MinimumSize = new Size(760, 420);
            BackColor = Theme.Bg; Font = new Font(Theme.Font, 9.5f);
            try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { }
            LoadNames();

            actions.Controls.AddRange(new Control[] { auto, secs, secLbl, exportBtn, scanBtn });
            header.Controls.Add(actions); header.Controls.Add(title);
            footRight.Controls.AddRange(new Control[] { logToggle, sniffLink });
            footer.Controls.Add(footRight); footer.Controls.Add(status);
            body.Controls.Add(empty);

            Controls.Add(body); Controls.Add(log); Controls.Add(footer); Controls.Add(header);

            menu.Items.Add(L.T("Rename…", "Preimenuj…"), null, (_, _) => RenameSelected());
            menu.Items.Add(L.T("Copy MAC", "Kopiraj MAC"), null, (_, _) => { if (selected?.Dev != null) Clipboard.SetText(selected.Dev.Mac); });

            scanBtn.Click += (_, _) => _ = ScanAsync();
            exportBtn.Click += (_, _) => ExportCsv();
            logToggle.LinkClicked += (_, _) => { log.Visible = !log.Visible; logToggle.Text = log.Visible ? L.T("Hide log", "Sakrij log") : L.T("Show log", "Prikaži log"); };
            sniffLink.LinkClicked += (_, _) => ToggleSniff();
            auto.CheckedChanged += (_, _) => { timer.Interval = (int)secs.Value * 1000; timer.Enabled = auto.Checked; };
            secs.ValueChanged += (_, _) => timer.Interval = (int)secs.Value * 1000;
            timer.Tick += (_, _) => _ = ScanAsync();
            body.Resize += (_, _) => FitCards();
            Shown += (_, _) => _ = ScanAsync();
            FormClosing += (_, _) => sniff?.Dispose();
        }

        // ---- names -------------------------------------------------------------------
        void LoadNames()
        {
            try
            {
                if (File.Exists(namesPath))
                    foreach (var kv in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(namesPath)) ?? new()) names[kv.Key] = kv.Value;
            }
            catch { }
        }

        void SaveNames()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(namesPath)!);
                File.WriteAllText(namesPath, JsonSerializer.Serialize(names));
            }
            catch (Exception ex) { Log(L.T("Could not save names: ", "Spremanje imena nije uspjelo: ") + ex.Message); }
        }

        string NameOf(string mac)
        {
            if (names.TryGetValue(mac, out var n) && n.Length > 0) return n;
            var parts = mac.Split(':');
            return "Device_" + (parts[^2] + parts[^1]).ToLowerInvariant();
        }

        void RenameSelected()
        {
            if (selected?.Dev == null) return;
            var mac = selected.Dev.Mac;
            using var dlg = new Form { Text = L.T("Rename device", "Preimenuj uređaj"), Width = 380, Height = 150, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var tb = new TextBox { Left = 16, Top = 16, Width = 330, Text = NameOf(mac), MaxLength = 40 };
            var ok = new Button { Text = L.T("Save", "Spremi"), Left = 266, Top = 56, Width = 80, DialogResult = DialogResult.OK };
            dlg.Controls.AddRange(new Control[] { tb, ok }); dlg.AcceptButton = ok;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            names[mac] = tb.Text.Trim(); SaveNames(); Render();
        }

        // ---- scanning ----------------------------------------------------------------
        void Log(string s)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(() => log.AppendText($"{DateTime.Now:HH:mm:ss}  {s}\r\n"));
        }

        async Task ScanAsync()
        {
            if (busy) return;
            busy = true; scanBtn.Enabled = false; scanBtn.Text = L.T("Scanning…", "Skeniram…"); status.Text = L.T("Scanning…", "Skeniranje…");
            try
            {
                var result = await Task.Run(() =>
                {
                    if (demo) return DemoDevices();
                    var all = new List<PlcDevice>();
                    var nics = PlcScanner.ListNics();
                    Log(L.T("Adapters: ", "Adapteri: ") + string.Join(", ", nics.Select(n => n.name)));
                    var tasks = nics.Select(n => Task.Run(() =>
                    {
                        try
                        {
                            using var s = new NicSession(n.dev, n.name, n.mac) { Log = Log };
                            var r = PlcScanner.Scan(s);
                            lock (all) all.AddRange(r);
                        }
                        catch (DllNotFoundException) { throw; }
                        catch (Exception ex) { Log($"[{n.name}] " + L.T("error: ", "greška: ") + ex.Message); }
                    })).ToArray();
                    Task.WaitAll(tasks);
                    return all;
                });
                devices = result;
                Render();
                status.Text = result.Count == 0 ? L.T("No powerline adapter found.", "Nije pronađen nijedan powerline adapter.") : L.T("Devices", "Uređaja") + $": {result.Count}   ·   " + L.T("last scan", "zadnje skeniranje") + $" {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                var inner = ex is AggregateException ae ? ae.InnerException ?? ex : ex;
                status.Text = L.T("Error", "Greška");
                MessageBox.Show(inner is DllNotFoundException
                    ? L.T("Npcap is not installed. Get it from https://npcap.com (tick 'WinPcap API-compatible mode').", "Npcap nije instaliran. Instaliraj ga s https://npcap.com (označi 'WinPcap API-compatible mode').")
                    : inner.Message, "Powerline Tool");
            }
            finally { busy = false; scanBtn.Enabled = true; scanBtn.Text = L.T("Scan", "Skeniraj"); }
        }

        // ---- rendering ---------------------------------------------------------------
        void Render()
        {
            var keep = selected?.Dev?.Mac;
            selected = null;
            body.SuspendLayout();
            foreach (Control c in body.Controls.OfType<DeviceCard>().ToList()) { body.Controls.Remove(c); c.Dispose(); }
            body.Controls.Remove(empty);

            if (devices.Count == 0)
            {
                empty.Text = L.T("No powerline adapter found.\r\nCheck the cable and press Scan.", "Nije pronađen nijedan powerline adapter.\r\nProvjeri kabel i klikni Skeniraj.");
                body.Controls.Add(empty);
            }
            foreach (var d in devices.OrderByDescending(d => d.IsLocal).ThenBy(d => d.Mac))
            {
                var card = new DeviceCard { ContextMenuStrip = menu };
                card.Bind(d, NameOf(d.Mac), NameOf);
                card.MouseDown += (_, _) => Select(card);
                body.Controls.Add(card);
                if (d.Mac == keep) Select(card);
            }
            FitCards();
            body.ResumeLayout();
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

        // ---- misc --------------------------------------------------------------------
        void ToggleSniff()
        {
            if (sniff != null)
            {
                sniff.Dispose(); sniff = null; sniffLink.Text = L.T("Capture traffic (diagnostics)", "Snimaj promet (dijagnostika)");
                Log(L.T("Capture stopped.", "Snimanje zaustavljeno."));
                return;
            }
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "powerline-sniff.txt");
            try
            {
                sniff = PlcScanner.StartSniff(path, Log);
                sniffLink.Text = L.T("Stop capture", "Zaustavi snimanje");
                log.Visible = true; logToggle.Text = L.T("Hide log", "Sakrij log");
                Log(L.T("Capturing to: ", "Snimanje u: ") + path);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void ExportCsv()
        {
            using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "powerline.csv" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var sb = new StringBuilder("Type,Name,MAC,Link to,TX,RX,Firmware\r\n");
            foreach (var d in devices)
            {
                var type = d.IsLocal ? "Local" : "Remote";
                if (d.LinkList.Count == 0) sb.AppendLine($"{type},\"{NameOf(d.Mac)}\",{d.Mac},,,,\"{d.Firmware}\"");
                foreach (var l in d.LinkList)
                    sb.AppendLine($"{type},\"{NameOf(d.Mac)}\",{d.Mac},{l.Peer},{l.Tx},{l.Rx},\"{d.Firmware}\"");
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        }
    }
}




