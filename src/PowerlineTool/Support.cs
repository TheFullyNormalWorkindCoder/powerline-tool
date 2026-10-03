using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace PowerlineTool;

/// <summary>Command-line switches: --demo --lang=en|hr --theme=light|dark --page=devices|map|history|settings|about --minimized --classic</summary>
record Options(bool Demo, string Lang, string Theme, string Page, bool Minimized, bool Classic = false, string Shot = null, bool DevTools = false, string Accent = null)
{
    public static Options Parse(string[] args)
    {
        string Value(string key) => args.FirstOrDefault(a => a.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))?[(key.Length + 1)..];
        return new Options(args.Contains("--demo"), Value("--lang"), Value("--theme"), Value("--page"), args.Contains("--minimized"),
                           args.Contains("--classic"), Value("--shot"), args.Contains("--devtools"), Value("--accent"));
    }
}

/// <summary>Optional "start with Windows": one value under HKCU\...\Run, removed again when switched off.</summary>
static class Startup
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "PowerlineTool";

    public static bool IsEnabled()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue(ValueName) != null; }
        catch { return false; }
    }

    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key, true);
        if (k == null) return;
        if (enabled) k.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --minimized");
        else k.DeleteValue(ValueName, false);
    }
}

/// <summary>A second launch signals the first one to come forward instead of starting another scanner.</summary>
static class SingleInstance
{
    public const string MutexName = "PowerlineTool.SingleInstance";
    public const string ShowEvent = "PowerlineTool.Show";

    public static void SignalFirst()
    {
        try { EventWaitHandle.OpenExisting(ShowEvent).Set(); } catch { }
    }

    /// <summary>Calls <paramref name="onShow"/> (on a thread-pool thread) whenever another launch asks for the window.</summary>
    public static IDisposable Listen(Action onShow)
    {
        var ev = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent);
        var reg = ThreadPool.RegisterWaitForSingleObject(ev, (_, _) => onShow(), null, -1, false);
        return new Handle(ev, reg);
    }

    sealed class Handle : IDisposable
    {
        readonly EventWaitHandle ev; readonly RegisteredWaitHandle reg;
        public Handle(EventWaitHandle e, RegisteredWaitHandle r) { ev = e; reg = r; }
        public void Dispose() { reg.Unregister(null); ev.Dispose(); }
    }
}

/// <summary>Manual "check for updates": asks GitHub for the latest release. Never runs on its own.</summary>
static class Updater
{
    public const string Repo = "TheFullyNormalWorkindCoder/powerline-tool";
    public static string RepoUrl => "https://github.com/" + Repo;

    public static string CurrentVersion =>
        (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    public static async Task<(bool Newer, string Tag, string Url)> CheckAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PowerlineTool/" + CurrentVersion);
        using var doc = JsonDocument.Parse(await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest"));
        var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        var url = doc.RootElement.GetProperty("html_url").GetString() ?? RepoUrl;
        bool newer = Version.TryParse(tag.TrimStart('v'), out var latest) && Version.TryParse(CurrentVersion, out var cur) && latest > cur;
        return (newer, tag, url);
    }

    /// <summary>Only addresses this project itself points users to are ever opened.</summary>
    public static bool IsAllowedUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps &&
        (u.Host == "npcap.com" || u.Host == "github.com" && u.AbsolutePath.StartsWith("/" + Repo, StringComparison.OrdinalIgnoreCase));

    public static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
