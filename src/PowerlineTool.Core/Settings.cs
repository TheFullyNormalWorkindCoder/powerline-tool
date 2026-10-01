using System.Text.Json;

namespace PowerlineTool.Core;

/// <summary>User settings and device names, stored as JSON in %APPDATA%\PowerlineTool.</summary>
public class Settings
{
    public bool AutoRefresh { get; set; }
    public int IntervalSec { get; set; } = 10;
    public string Theme { get; set; } = "system";       // system | light | dark
    public string Language { get; set; } = "auto";      // auto | en | hr
    public int WarnBelowMbps { get; set; } = 50;
    public bool Notify { get; set; } = true;
    public bool MinimizeToTray { get; set; }
    public bool LogHistory { get; set; }
    public int Width { get; set; } = 1040;
    public int Height { get; set; } = 700;
    public Dictionary<string, string> Names { get; set; } = new();

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PowerlineTool");
    public static string DefaultPath => Path.Combine(Dir, "settings.json");
    public static string HistoryPath => Path.Combine(Dir, "history.csv");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string NameOf(string mac) => Names.TryGetValue(mac, out var n) && n.Length > 0 ? n : MacUtil.DefaultName(mac);

    public static Settings Load(string path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
                return Normalize(JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings());

            // Version 1.0 kept only the device names, in names.json.
            var legacy = Path.Combine(Path.GetDirectoryName(path)!, "names.json");
            if (File.Exists(legacy))
            {
                var s = new Settings();
                foreach (var kv in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(legacy)) ?? new()) s.Names[kv.Key] = kv.Value;
                return s;
            }
        }
        catch { /* corrupt file: fall back to defaults rather than refusing to start */ }
        return new Settings();
    }

    static Settings Normalize(Settings s)
    {
        s.IntervalSec = Math.Clamp(s.IntervalSec, 3, 600);
        s.WarnBelowMbps = Math.Clamp(s.WarnBelowMbps, 1, 1000);
        s.Width = Math.Clamp(s.Width, 760, 4000);
        s.Height = Math.Clamp(s.Height, 460, 3000);
        s.Names ??= new();
        return s;
    }

    public void Save(string path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }
}
