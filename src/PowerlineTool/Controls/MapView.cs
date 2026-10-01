using System.Drawing.Drawing2D;
using PowerlineTool.Core;

namespace PowerlineTool.Controls;

/// <summary>Network map: adapters as nodes, links as lines whose colour and thickness follow the speed.</summary>
class MapView : Control
{
    public List<PlcDevice> Devices = new();
    public Func<string, string> NameOf = m => m;

    public MapView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    Dictionary<string, PointF> Positions()
    {
        var pos = new Dictionary<string, PointF>();
        var list = Devices.OrderByDescending(d => d.IsLocal).ThenBy(d => d.Mac).ToList();
        float cx = Width / 2f, cy = Height / 2f - 6;
        float r = Math.Max(60, Math.Min(Width / 2f - 130, Height / 2f - 90));
        if (list.Count == 1) pos[list[0].Mac] = new PointF(cx, cy);
        else if (list.Count == 2)
        {
            float dx = Math.Min(r * 1.5f, Width / 2f - 110);
            pos[list[0].Mac] = new PointF(cx - dx, cy); pos[list[1].Mac] = new PointF(cx + dx, cy);
        }
        else
            for (int i = 0; i < list.Count; i++)
            {
                double a = -Math.PI / 2 + i * 2 * Math.PI / list.Count;
                pos[list[i].Mac] = new PointF(cx + (float)(Math.Cos(a) * r * 1.4), cy + (float)(Math.Sin(a) * r));
            }
        return pos;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(Theme.Bg);

        if (Devices.Count == 0)
        {
            using var f = new Font(Theme.Font, 11f);
            TextRenderer.DrawText(g, L.T("No devices yet. Press Scan.", "Još nema uređaja. Klikni Skeniraj."), f, ClientRectangle, Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var pos = Positions();

        // links first, so nodes are drawn on top
        var done = new HashSet<string>();
        foreach (var d in Devices)
            foreach (var l in d.Links)
            {
                if (!pos.ContainsKey(l.Peer)) continue;
                var key = string.CompareOrdinal(d.Mac, l.Peer) < 0 ? d.Mac + l.Peer : l.Peer + d.Mac;
                if (!done.Add(key)) continue;
                var a = pos[d.Mac]; var b = pos[l.Peer];
                float w = Math.Clamp(2 + l.Min / 50f, 2, 9);
                using (var pen = new Pen(Theme.RateColor(l.Min), w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pen, a, b);

                // Labels sit nearer the far end so that links leaving the same node do not pile up on each other.
                float t = Devices.Count == 2 ? 0.5f : 0.6f;
                var mid = new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
                var text = $"→ {NameOf(l.Peer)}   TX {l.Tx} · RX {l.Rx}";
                using var f = new Font(Theme.Font, 8.5f, FontStyle.Bold);
                var sz = TextRenderer.MeasureText(text, f);
                var rect = new Rectangle((int)mid.X - sz.Width / 2 - 8, (int)mid.Y - 12, sz.Width + 16, 24);
                using (var path = DeviceCard.Round(rect, 12))
                using (var fill = new SolidBrush(Theme.Card))
                using (var pen = new Pen(Theme.RateColor(l.Min), 1.5f))
                { g.FillPath(fill, path); g.DrawPath(pen, path); }
                TextRenderer.DrawText(g, text, f, rect, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

        foreach (var d in Devices)
        {
            var p = pos[d.Mac];
            const int r = 34;
            using (var ring = new SolidBrush(Color.FromArgb(Theme.Dark ? 50 : 40, d.IsLocal ? Theme.Teal : Theme.TealDark)))
                g.FillEllipse(ring, p.X - r - 7, p.Y - r - 7, (r + 7) * 2, (r + 7) * 2);
            using (var fill = new SolidBrush(d.IsLocal ? Theme.Teal : Theme.TealDark))
                g.FillEllipse(fill, p.X - r, p.Y - r, r * 2, r * 2);
            using (var f = new Font(Theme.Font, 11f, FontStyle.Bold))
                TextRenderer.DrawText(g, "PLC", f, new Rectangle((int)p.X - r, (int)p.Y - r, r * 2, r * 2), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (var back = new SolidBrush(Theme.Bg))
                g.FillRectangle(back, p.X - 112, p.Y + r + 6, 224, 44);   // keeps link lines from striking through the text
            using (var f = new Font(Theme.Font, 10f, FontStyle.Bold))
                TextRenderer.DrawText(g, NameOf(d.Mac), f, new Rectangle((int)p.X - 100, (int)p.Y + r + 8, 200, 20), Theme.Text, TextFormatFlags.HorizontalCenter);
            using (var f = new Font("Consolas", 8.5f))
                TextRenderer.DrawText(g, d.IsLocal ? d.Mac + "  (" + L.T("local", "lokalni") + ")" : d.Mac, f,
                    new Rectangle((int)p.X - 110, (int)p.Y + r + 28, 220, 18), Theme.Muted, TextFormatFlags.HorizontalCenter);
        }
    }
}

