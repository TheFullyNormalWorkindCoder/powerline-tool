using System.Text.Json;

namespace PowerlineTool.Core;

/// <summary>Things the user interface needs to know that the coordinator does not keep.</summary>
public record UiFlags(string Version, bool Demo, DateTime? NextScanAt, bool Sniffing, bool NpcapMissing, bool StartWithWindows);

/// <summary>Turns the application state into the JSON the web interface renders. Property names are camelCase.</summary>
public static class UiState
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never };

    /// <summary>Samples per link sent to the UI. 720 at a 5 s interval is one hour.</summary>
    public const int HistoryPerLink = 720;

    public static long Ms(DateTime t) => t == default ? 0 : new DateTimeOffset(t.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(t, DateTimeKind.Local) : t).ToUnixTimeMilliseconds();

    public static object Build(ScanCoordinator c, UiFlags f)
    {
        var s = c.Settings;
        var warnings = new List<string>(c.Last.Warnings);
        if (f.NpcapMissing && !warnings.Contains("npcap_missing")) warnings.Add("npcap_missing");

        var history = new Dictionary<string, object>();
        foreach (var (a, b) in c.History.Pairs)
        {
            var series = c.History.Series(a, b);
            var tail = series.Count > HistoryPerLink ? series.Skip(series.Count - HistoryPerLink) : series;
            history[a + "|" + b] = tail.Select(x => new[] { Ms(x.Time), x.Tx, x.Rx }).ToList();
        }

        return new
        {
            type = "state",
            state = new
            {
                app = new { version = f.Version, demo = f.Demo },
                scanning = c.Scanning,
                lastScan = c.LastScan is { } ls ? (long?)Ms(ls) : null,
                nextScanAt = f.NextScanAt is { } ns ? (long?)Ms(ns) : null,
                sniffing = f.Sniffing,
                nics = c.Last.Nics,
                skipped = c.Last.Skipped,
                warnings,
                devices = c.Devices.Select(d => new
                {
                    mac = d.Mac, name = s.NameOf(d.Mac), local = d.IsLocal, role = d.Role, firmware = d.Firmware, nid = d.Nid, tei = d.Tei,
                    nic = d.Nic, stale = d.Stale, missed = d.Missed, linksStale = d.LinksStale, lastSeen = Ms(d.LastSeen),
                    links = d.Links.Select(l => new
                    {
                        peer = l.Peer, peerName = s.NameOf(l.Peer), tx = l.Tx, rx = l.Rx, quality = Quality.Of(l.Min).ToString().ToLowerInvariant(),
                    }).ToList(),
                }).ToList(),
                history,
                events = c.Events.Select(e => new { ts = Ms(e.Time), kind = e.Kind.ToString(), mac = e.Mac, peer = e.Peer, mbps = e.Mbps }).ToList(),
                settings = new
                {
                    s.AutoRefresh, s.IntervalSec, s.Theme, s.Language, s.Accent, s.WarnBelowMbps, s.Notify, s.MinimizeToTray,
                    StartWithWindows = f.StartWithWindows, s.LogHistory, s.ReduceMotion, s.AlwaysOnTop, Ui = s.Ui,
                },
            },
        };
    }

    public static string Serialize(ScanCoordinator c, UiFlags f) => JsonSerializer.Serialize(Build(c, f), Json);
}
