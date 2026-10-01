namespace PowerlineTool.Core;

public enum ChangeKind { DeviceAppeared, DeviceLost, LinkLow, LinkRecovered }

public record NetworkChange(ChangeKind Kind, string Mac, string Peer = "", int Mbps = 0);

/// <summary>Compares two consecutive scans and reports what a user would want to be told about.</summary>
public static class ChangeDetector
{
    public static List<NetworkChange> Diff(IReadOnlyCollection<PlcDevice> prev, IReadOnlyCollection<PlcDevice> next, int warnBelowMbps)
    {
        var changes = new List<NetworkChange>();
        if (prev == null || prev.Count == 0) return changes; // first scan: nothing to compare with

        var before = prev.ToDictionary(d => d.Mac);
        var after = next.ToDictionary(d => d.Mac);

        foreach (var d in next.Where(d => !before.ContainsKey(d.Mac))) changes.Add(new NetworkChange(ChangeKind.DeviceAppeared, d.Mac));
        foreach (var d in prev.Where(d => !after.ContainsKey(d.Mac))) changes.Add(new NetworkChange(ChangeKind.DeviceLost, d.Mac));

        var reported = new HashSet<string>();
        foreach (var d in next)
            foreach (var l in d.Links)
            {
                var key = string.CompareOrdinal(d.Mac, l.Peer) < 0 ? d.Mac + "|" + l.Peer : l.Peer + "|" + d.Mac;
                if (!reported.Add(key)) continue;

                int? old = before.TryGetValue(d.Mac, out var p) ? p.Links.FirstOrDefault(x => x.Peer == l.Peer)?.Min : null;
                bool nowLow = l.Min < warnBelowMbps;
                bool wasLow = old.HasValue && old.Value < warnBelowMbps;
                if (old == null) { if (nowLow) changes.Add(new NetworkChange(ChangeKind.LinkLow, d.Mac, l.Peer, l.Min)); continue; }
                if (nowLow && !wasLow) changes.Add(new NetworkChange(ChangeKind.LinkLow, d.Mac, l.Peer, l.Min));
                else if (!nowLow && wasLow) changes.Add(new NetworkChange(ChangeKind.LinkRecovered, d.Mac, l.Peer, l.Min));
            }
        return changes;
    }
}
