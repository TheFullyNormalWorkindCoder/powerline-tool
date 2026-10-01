namespace PowerlineTool.Forms;

class AboutForm : Form
{
    public AboutForm(Icon icon)
    {
        Text = L.T("About Powerline Tool", "O programu Powerline Tool");
        Width = 460; Height = 330; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        Font = new Font(Theme.Font, 9.5f);

        var pic = new PictureBox { Left = 24, Top = 24, Width = 64, Height = 64, SizeMode = PictureBoxSizeMode.Zoom };
        try { pic.Image = new Icon(icon, 64, 64).ToBitmap(); } catch { }
        var name = new Label { Text = "Powerline Tool", Left = 104, Top = 24, AutoSize = true, Font = new Font(Theme.Font, 16f, FontStyle.Bold) };
        var ver = new Label { Text = L.T("Version ", "Verzija ") + Updater.CurrentVersion, Left = 106, Top = 62, AutoSize = true, ForeColor = Theme.Muted };
        var text = new Label
        {
            Left = 24, Top = 110, Width = 410, Height = 90,
            Text = L.T("A small, reliable replacement for TP-Link's tpPLC utility. Read-only: it never changes anything on your adapters.\n\nMIT licence. Not affiliated with TP-Link.",
                       "Mala, pouzdana zamjena za TP-Linkov tpPLC. Samo čita: ne mijenja ništa na tvojim adapterima.\n\nMIT licenca. Nije povezano s TP-Linkom."),
        };
        var link = new LinkLabel { Text = Updater.RepoUrl, Left = 24, Top = 206, AutoSize = true };
        link.LinkClicked += (_, _) => Updater.Open(Updater.RepoUrl);
        var issues = new LinkLabel { Text = L.T("Report a problem or an adapter model", "Prijavi problem ili model adaptera"), Left = 24, Top = 230, AutoSize = true };
        issues.LinkClicked += (_, _) => Updater.Open(Updater.RepoUrl + "/issues/new/choose");
        var ok = new Button { Text = "OK", Left = 354, Top = 248, Width = 80, Height = 30, DialogResult = DialogResult.OK };

        Controls.AddRange(new Control[] { pic, name, ver, text, link, issues, ok });
        AcceptButton = ok;
        Dialogs.Style(this);
        ver.ForeColor = Theme.Muted;
        HandleCreated += (_, _) => Theme.TitleBar(this);
    }
}
