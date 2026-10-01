using PowerlineTool.Core;

namespace PowerlineTool.Forms;

static class Dialogs
{
    public static void Style(Control root)
    {
        root.BackColor = Theme.Bg; root.ForeColor = Theme.Text;
        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case TextBox or ComboBox or NumericUpDown: c.BackColor = Theme.InputBg; c.ForeColor = Theme.Text; break;
                case LinkLabel ll: ll.LinkColor = Theme.TealText; ll.ActiveLinkColor = Theme.Teal; ll.ForeColor = Theme.Text; break;
                case Button b: b.FlatStyle = FlatStyle.Flat; b.BackColor = Theme.Teal; b.ForeColor = Color.White; b.FlatAppearance.BorderSize = 0; break;
                default: c.ForeColor = Theme.Text; break;
            }
            if (c.HasChildren) Style(c);
        }
    }

    public static string Prompt(IWin32Window owner, string title, string label, string initial)
    {
        using var f = new Form
        {
            Text = title, Width = 400, Height = 170, StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            Font = new Font(Theme.Font, 9.5f),
        };
        var lbl = new Label { Text = label, Left = 16, Top = 14, AutoSize = true };
        var tb = new TextBox { Left = 16, Top = 40, Width = 352, Text = initial, MaxLength = 40 };
        var ok = new Button { Text = L.T("Save", "Spremi"), Left = 288, Top = 82, Width = 80, Height = 30, DialogResult = DialogResult.OK };
        f.Controls.AddRange(new Control[] { lbl, tb, ok });
        f.AcceptButton = ok;
        Style(f);
        f.HandleCreated += (_, _) => Theme.TitleBar(f);
        f.Shown += (_, _) => { tb.SelectAll(); tb.Focus(); };
        return f.ShowDialog(owner) == DialogResult.OK ? tb.Text.Trim() : null;
    }
}
