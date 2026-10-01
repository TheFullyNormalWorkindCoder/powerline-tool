using System.Drawing.Drawing2D;
using PowerlineTool.Core;

namespace PowerlineTool.Controls;

/// <summary>One adapter: name, MAC, badge and a TX/RX bar pair for each of its links.</summary>
class DeviceCard : Control
{
    const int HeaderH = 76, RowH = 34;
    public PlcDevice Dev;
    public Func<string, string> NameOf = m => m;
    public bool Selected;

    public DeviceCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Margin = new Padding(0, 0, 0, 12);
    }

    public void Bind(PlcDevice d, Func<string, string> nameOf)
    {
        Dev = d; NameOf = nameOf;
        Height = HeaderH + Math.Max(1, d.Links.Count) * RowH + 10;
        Invalidate();
    }

    public string Details() => string.Join("\n", new[]
    {
        $"{NameOf(Dev.Mac)}  ({Dev.Mac})",
        L.T("Firmware", "Firmware") + ": " + (Dev.Firmware == "" ? "-" : Dev.Firmware),
        L.T("Network ID", "ID mreže") + ": " + (Dev.Nid == "" ? "-" : Dev.Nid),
        "TEI: " + Dev.Tei + (Dev.Role == "" ? "" : "   " + L.T("Role", "Uloga") + ": " + Dev.Role),
        L.T("Seen via", "Viđen preko") + ": " + Dev.Nic,
    });

    public static GraphicsPath Round(Rectangle r, int rad)
    {
        var p = new GraphicsPath(); int d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    static void Bar(Graphics g, int x, int y, int w, int h, int value)
    {
        using var track = Round(new Rectangle(x, y, w, h), h / 2);
        using var tb = new SolidBrush(Theme.Track);
        g.FillPath(tb, track);
        int fw = (int)(w * Math.Min(1.0, value / 600.0));
        if (fw < h) fw = Math.Min(w, h);
        using var fill = Round(new Rectangle(x, y, fw, h), h / 2);
        using var fb = new SolidBrush(Theme.RateColor(value));
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

        using (var ib = new SolidBrush(Dev.IsLocal ? Theme.Teal : Theme.TealDark))
            g.FillEllipse(ib, 18, 16, 42, 42);
        using (var f = new Font(Theme.Font, 9f, FontStyle.Bold))
            TextRenderer.DrawText(g, "PLC", f, new Rectangle(18, 16, 42, 42), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        using (var f = new Font(Theme.Font, 12f, FontStyle.Bold))
            TextRenderer.DrawText(g, NameOf(Dev.Mac), f, new Point(72, 14), Theme.Text);
        using (var f = new Font("Consolas", 9f))
            TextRenderer.DrawText(g, Dev.Mac, f, new Point(74, 40), Theme.Muted);

        var badge = Dev.IsLocal ? L.T("LOCAL", "LOKALNI") : L.T("REMOTE", "UDALJENI");
        using (var f = new Font(Theme.Font, 8f, FontStyle.Bold))
        {
            var sz = TextRenderer.MeasureText(badge, f);
            var br = new Rectangle(Width - 20 - sz.Width - 14, 16, sz.Width + 14, 22);
            using var bp = Round(br, 11);
            using var bb = new SolidBrush(Dev.IsLocal ? Color.FromArgb(Theme.Dark ? 60 : 255, Theme.Dark ? Theme.Teal : Color.FromArgb(0xD9, 0xF3, 0xF5)) : Theme.Track);
            g.FillPath(bb, bp);
            TextRenderer.DrawText(g, badge, f, br, Dev.IsLocal ? Theme.TealText : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        using (var f = new Font(Theme.Font, 8.5f))
        {
            var fw = Dev.Firmware == "" ? "" : "FW " + Dev.Firmware;
            var sz = TextRenderer.MeasureText(fw, f);
            TextRenderer.DrawText(g, fw, f, new Point(Width - 20 - sz.Width, 42), Theme.Muted);
        }

        using (var sep = new Pen(Theme.Border)) g.DrawLine(sep, 18, HeaderH - 2, Width - 18, HeaderH - 2);

        if (Dev.Links.Count == 0)
        {
            using var f = new Font(Theme.Font, 9f, FontStyle.Italic);
            TextRenderer.DrawText(g, L.T("No link data", "Nema podataka o vezi"), f, new Point(20, HeaderH + 8), Theme.Muted);
            return;
        }

        int x0 = 230, right = Width - 20;
        int half = (right - x0) / 2;
        for (int i = 0; i < Dev.Links.Count; i++)
        {
            var link = Dev.Links[i];
            int y = HeaderH + i * RowH;
            using var lf = new Font(Theme.Font, 9.5f);
            TextRenderer.DrawText(g, "→  " + NameOf(link.Peer), lf, new Point(20, y + 6), Theme.Text);

            for (int k = 0; k < 2; k++)
            {
                int bx = x0 + k * half, val = k == 0 ? link.Tx : link.Rx;
                using var cf = new Font(Theme.Font, 8f, FontStyle.Bold);
                TextRenderer.DrawText(g, k == 0 ? "TX" : "RX", cf, new Point(bx, y + 9), Theme.Muted);
                int barW = Math.Max(50, half - 36 - 90);
                Bar(g, bx + 26, y + 11, barW, 10, val);
                using var vf = new Font(Theme.Font, 9.5f, FontStyle.Bold);
                TextRenderer.DrawText(g, $"{val} Mbps", vf, new Point(bx + 26 + barW + 8, y + 5), Theme.RateColor(val));
            }
        }
    }
}
