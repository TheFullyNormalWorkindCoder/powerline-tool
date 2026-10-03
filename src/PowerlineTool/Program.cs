using Microsoft.Web.WebView2.Core;
using PowerlineTool.Core;
using PowerlineTool.Forms;
using PowerlineTool.Web;

namespace PowerlineTool;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var opts = Options.Parse(args);

        // One instance only: two scanners at once would confuse each other. The second launch just wakes the first.
        using var mutex = new Mutex(true, SingleInstance.MutexName, out bool first);
        if (!first && !opts.Demo)
        {
            SingleInstance.SignalFirst();
            return;
        }

        // Demo mode never touches the saved settings, so command-line overrides can safely become the settings.
        var settings = opts.Demo ? new Settings() : Settings.Load();
        if (opts.Demo)
        {
            if (opts.Lang != null) settings.Language = opts.Lang;
            if (opts.Theme != null) settings.Theme = opts.Theme;
            if (opts.Accent != null) settings.Accent = opts.Accent;
        }
        L.Override = opts.Lang ?? settings.Language;
        Theme.Apply(Theme.Resolve(opts.Theme ?? settings.Theme));

        bool wantWeb = !opts.Classic && settings.Ui != "classic" && WebViewAvailable();
        if (wantWeb)
        {
            var web = new WebMainForm(settings, opts);
            Application.Run(web);
            if (!web.FallbackToClassic) return;
            MessageBox.Show(L.T("The modern interface needs the Microsoft Edge WebView2 runtime, which could not be started. Opening the classic window instead.\n\n",
                                "Moderno sučelje treba Microsoft Edge WebView2 runtime, koji se nije mogao pokrenuti. Otvaram klasični prozor.\n\n") + web.FallbackReason,
                            "Powerline Tool");
        }
        Application.Run(new MainForm(settings, opts));
    }

    static bool WebViewAvailable()
    {
        try { return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); }
        catch { return false; }
    }
}
