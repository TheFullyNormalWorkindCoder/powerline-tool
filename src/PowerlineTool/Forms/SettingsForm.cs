using System.Diagnostics;
using PowerlineTool.Core;

namespace PowerlineTool.Forms;

class SettingsForm : Form
{
    readonly Settings s;
    readonly ComboBox theme = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    readonly ComboBox lang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    readonly NumericUpDown warn = new() { Minimum = 1, Maximum = 1000, Width = 80 };
    readonly CheckBox notify = new() { AutoSize = true };
    readonly CheckBox tray = new() { AutoSize = true };
    readonly CheckBox log = new() { AutoSize = true };
    readonly CheckBox startup = new() { AutoSize = true };

    public SettingsForm(Settings s)
    {
        this.s = s;
        Text = L.T("Settings", "Postavke"); Width = 520; Height = 470; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        Font = new Font(Theme.Font, 9.5f);

        theme.Items.AddRange(new object[] { L.T("Follow Windows", "Prema Windowsima"), L.T("Light", "Svijetla"), L.T("Dark", "Tamna") });
        theme.SelectedIndex = s.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        lang.Items.AddRange(new object[] { L.T("Automatic", "Automatski"), "English", "Hrvatski" });
        lang.SelectedIndex = s.Language switch { "en" => 1, "hr" => 2, _ => 0 };
        warn.Value = s.WarnBelowMbps;
        notify.Checked = s.Notify; tray.Checked = s.MinimizeToTray; log.Checked = s.LogHistory; startup.Checked = Startup.IsEnabled();

        int y = 20;
        void Row(string label, Control c, string hint = null)
        {
            Controls.Add(new Label { Text = label, Left = 20, Top = y + 4, AutoSize = true, MaximumSize = new Size(250, 0) });
            c.Left = 290; c.Top = y; Controls.Add(c);
            y += 36;
            if (hint != null) { Controls.Add(new Label { Text = hint, Left = 20, Top = y - 8, AutoSize = true, ForeColor = Theme.Muted, Font = new Font(Theme.Font, 8.5f), MaximumSize = new Size(470, 0) }); y += 22; }
        }

        Row(L.T("Theme", "Tema"), theme);
        Row(L.T("Language", "Jezik"), lang, L.T("Language changes apply after restarting the app.", "Promjena jezika vrijedi nakon ponovnog pokretanja."));
        Row(L.T("Warn when a link drops below (Mbps)", "Upozori kad veza padne ispod (Mbps)"), warn);
        Row(L.T("Show notifications (device lost, link drop)", "Prikaži obavijesti (uređaj nestao, pad veze)"), notify);
        Row(L.T("Keep running in the tray when closed", "Nastavi raditi u traci kad zatvoriš prozor"), tray);
        Row(L.T("Save speed history to a CSV file", "Spremaj povijest brzina u CSV datoteku"), log,
            L.T("Written to the data folder, one row per link per scan.", "Sprema se u mapu s podacima, jedan redak po vezi po skeniranju."));
        Row(L.T("Start with Windows", "Pokreni uz Windowse"), startup,
            L.T("Starts minimised. Combine with the tray option to run quietly in the background.", "Pokreće se smanjeno. Uz opciju trake radi tiho u pozadini."));

        var folder = new LinkLabel { Text = L.T("Open data folder", "Otvori mapu s podacima"), Left = 20, Top = y + 4, AutoSize = true };
        folder.LinkClicked += (_, _) => { Directory.CreateDirectory(Settings.Dir); Process.Start("explorer.exe", Settings.Dir); };
        var ok = new Button { Text = "OK", Left = 330, Top = y, Width = 80, Height = 32, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = L.T("Cancel", "Odustani"), Left = 416, Top = y, Width = 80, Height = 32, DialogResult = DialogResult.Cancel };
        Controls.AddRange(new Control[] { folder, ok, cancel });
        AcceptButton = ok; CancelButton = cancel;
        ClientSize = new Size(520, y + 56);

        Dialogs.Style(this);
        HandleCreated += (_, _) => Theme.TitleBar(this);
    }

    public void Apply()
    {
        s.Theme = theme.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };
        s.Language = lang.SelectedIndex switch { 1 => "en", 2 => "hr", _ => "auto" };
        s.WarnBelowMbps = (int)warn.Value;
        s.Notify = notify.Checked; s.MinimizeToTray = tray.Checked; s.LogHistory = log.Checked;
        try { Startup.Set(startup.Checked); } catch { /* registry blocked by policy: ignore */ }
    }
}
