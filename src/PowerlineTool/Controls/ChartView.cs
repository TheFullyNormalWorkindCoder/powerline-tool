using System.Drawing.Drawing2D;
using PowerlineTool.Core;

namespace PowerlineTool.Controls;

/// <summary>Line chart of TX and RX over time for one link.</summary>
class ChartView : Control
{
    public IReadOnlyList<Sample> Data = Array.Empty<Sample>();
    public int WarnLine;

    public ChartView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(Theme.Bg);

        var box = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = DeviceCard.Round(box, 12))
        using (var fill = new SolidBrush(Theme.Card))
        using (var pen = new Pen(Theme.Border))
        { g.FillPath(fill, path); g.DrawPath(pen, path); }

        using var small = new Font(Theme.Font, 8.5f);
        if (Data.Count < 2)
        {
            TextRenderer.DrawText(g, L.F("Collecting data… ({0} sample(s), need at least 2)", "Prikupljam podatke… ({0} uzoraka, treba barem 2)", Data.Count),
                small, box, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        const int left = 58, right = 24, top = 38, bottom = 34;
        var plot = new Rectangle(left, top, Width - left - right, Height - top - bottom);
        if (plot.Width < 50 || plot.Height < 40) return;

        int peak = Math.Max(Data.Max(s => s.Tx), Data.Max(s => s.Rx));
        int step = peak <= 120 ? 25 : peak <= 400 ? 50 : 100;
        int ymax = Math.Max(step * 2, (int)Math.Ceiling(peak * 1.1 / step) * step);

        float Y(int v) => plot.Bottom - (float)v / ymax * plot.Height;
        var t0 = Data[0].Time; var span = Math.Max(1, (Data[^1].Time - t0).TotalSeconds);
        float X(DateTime t) => plot.Left + (float)((t - t0).TotalSeconds / span) * plot.Width;

        using (var grid = new Pen(Theme.Border))
            for (int v = 0; v <= ymax; v += step)
            {
                float y = Y(v);
                g.DrawLine(grid, plot.Left, y, plot.Right, y);
                TextRenderer.DrawText(g, v.ToString(), small, new Rectangle(0, (int)y - 9, left - 8, 18), Theme.Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        TextRenderer.DrawText(g, "Mbps", small, new Point(10, 10), Theme.Muted);

        foreach (var frac in new[] { 0.0, 0.5, 1.0 })
        {
            var t = t0.AddSeconds(span * frac);
            var sz = TextRenderer.MeasureText(t.ToString("HH:mm:ss"), small);
            int x = plot.Left + (int)(plot.Width * frac) - (frac == 0 ? 0 : frac == 1 ? sz.Width : sz.Width / 2);
            TextRenderer.DrawText(g, t.ToString("HH:mm:ss"), small, new Point(x, plot.Bottom + 8), Theme.Muted);
        }

        if (WarnLine > 0 && WarnLine < ymax)
            using (var warn = new Pen(Theme.Bad, 1.2f) { DashStyle = DashStyle.Dash })
                g.DrawLine(warn, plot.Left, Y(WarnLine), plot.Right, Y(WarnLine));

        PointF[] Series(Func<Sample, int> pick) => Data.Select(s => new PointF(X(s.Time), Y(pick(s)))).ToArray();
        var tx = Series(s => s.Tx); var rx = Series(s => s.Rx);

        var area = tx.Concat(new[] { new PointF(tx[^1].X, plot.Bottom), new PointF(tx[0].X, plot.Bottom) }).ToArray();
        using (var ab = new SolidBrush(Color.FromArgb(Theme.Dark ? 40 : 35, Theme.Teal))) g.FillPolygon(ab, area);

        using (var p = new Pen(Theme.Accent2, 2.2f) { LineJoin = LineJoin.Round }) g.DrawLines(p, rx);
        using (var p = new Pen(Theme.Teal, 2.6f) { LineJoin = LineJoin.Round }) g.DrawLines(p, tx);

        int lx = left;
        foreach (var (color, name) in new[] { (Theme.Teal, "TX"), (Theme.Accent2, "RX") })
        {
            using (var b = new SolidBrush(color)) g.FillEllipse(b, lx, 14, 10, 10);
            TextRenderer.DrawText(g, name, small, new Point(lx + 14, 10), Theme.Text);
            lx += 56;
        }
        if (WarnLine > 0)
            TextRenderer.DrawText(g, L.F("--- warning below {0} Mbps", "--- upozorenje ispod {0} Mbps", WarnLine), small, new Point(lx, 10), Theme.Bad);
    }
}
