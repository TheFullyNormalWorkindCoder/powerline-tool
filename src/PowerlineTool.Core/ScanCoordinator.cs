namespace PowerlineTool.Core;

public record AppEvent(DateTime Time, ChangeKind Kind, string Mac, string Peer, int Mbps);

public record ScanOutcome(ScanResult Result, List<NetworkChange> Changes);

/// <summary>
/// Everything about "do a scan and keep the books" that is not user interface: running the scanner, retrying an
/// empty result, remembering adapters, recording history and noticing what changed. Both UIs sit on top of this.
/// </summary>
public class ScanCoordinator
{
    readonly Func<ScanResult> scanner;
    readonly Func<DateTime> clock;
    readonly object gate = new();
    bool scanning;

    public Settings Settings { get; }
    public HistoryStore History { get; } = new();
    public DeviceMemory Memory { get; } = new();

    public List<PlcDevice> Devices { get; private set; } = new();
    public ScanResult Last { get; private set; } = new();
    public DateTime? LastScan { get; private set; }
    public List<AppEvent> Events { get; } = new();
    public bool Scanning { get { lock (gate) return scanning; } }

    /// <summary>Pause between the first (empty) scan and the automatic second try.</summary>
    public int RetryDelayMs { get; set; } = 400;

    public ScanCoordinator(Settings settings, Func<ScanResult> scanner, Func<DateTime> clock = null)
    {
        Settings = settings; this.scanner = scanner; this.clock = clock ?? (() => DateTime.Now);
    }

    /// <summary>Runs one scan. Returns null when another scan is already running.</summary>
    public async Task<ScanOutcome> ScanAsync()
    {
        lock (gate) { if (scanning) return null; scanning = true; }
        try
        {
            var result = await Task.Run(scanner);

            // A single empty answer after we have seen adapters is far more often a lost frame than a real outage.
            if (result.Devices.Count == 0 && Memory.HasDevices && !result.Warnings.Contains("no_nic"))
            {
                await Task.Delay(RetryDelayMs);
                var second = await Task.Run(scanner);
                if (second.Devices.Count > 0) result = second;
            }

            var now = clock();
            var before = Devices;
            Devices = Memory.Update(result.Devices, now);
            Last = result; LastScan = now;

            var fresh = Devices.Where(d => !d.Stale && !d.LinksStale).ToList();
            History.Add(now, fresh);
            if (Settings.LogHistory)
                try { HistoryCsv.Append(Settings.HistoryPath, now, fresh); } catch { /* disk full / locked: history is optional */ }

            var changes = ChangeDetector.Diff(before, Devices, Settings.WarnBelowMbps);
            foreach (var c in changes) Events.Add(new AppEvent(now, c.Kind, c.Mac, c.Peer, c.Mbps));
            if (Events.Count > 100) Events.RemoveRange(0, Events.Count - 100);

            if (Devices.Any(d => d.Stale) && !result.Warnings.Contains("stale")) result.Warnings.Add("stale");
            return new ScanOutcome(result, changes);
        }
        finally { lock (gate) scanning = false; }
    }

    public void ClearHistory() => History.Clear();
}
