namespace PowerlineTool.Controls;

/// <summary>Flat tab-style button with an underline when active.</summary>
class NavButton : Button
{
    bool active;
    public bool Active { get => active; set { active = value; Invalidate(); } }

    public NavButton()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand; Height = 42; Width = 110;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Card);
        using var f = new Font(Theme.Font, 10f, FontStyle.Bold);
        TextRenderer.DrawText(g, Text, f, ClientRectangle, active ? Theme.TealText : Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (active) using (var b = new SolidBrush(Theme.Teal)) g.FillRectangle(b, 12, Height - 3, Width - 24, 3);
    }
}
