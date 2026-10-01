using PowerlineTool.Core;
using PowerlineTool.Forms;

namespace PowerlineTool;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var opts = Options.Parse(args);

        // One instance only: two scanners at once would confuse each other. The second launch just wakes the first.
        using var mutex = new Mutex(true, "PowerlineTool.SingleInstance", out bool first);
        if (!first && !opts.Demo)
        {
            try { EventWaitHandle.OpenExisting("PowerlineTool.Show").Set(); } catch { }
            return;
        }

        var settings = opts.Demo ? new Settings() : Settings.Load();
        L.Override = opts.Lang ?? settings.Language;
        Theme.Apply(Theme.Resolve(opts.Theme ?? settings.Theme));
        Application.Run(new MainForm(settings, opts));
    }
}
