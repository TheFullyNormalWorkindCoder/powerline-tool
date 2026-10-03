namespace PowerlineTool.Core;

/// <summary>
/// Remembers adapters between scans. Powerline replies are occasionally lost (another program talking to the same
/// adapters, a brief link flap), and one empty scan must not wipe the screen. An adapter that stops answering stays
/// listed, flagged as stale, until it has been missing for <see cref="MaxMissed"/> scans in a row.
/// </summary>
public class DeviceMemory
{
    readonly Dictionary<string, PlcDevice> known = new();
    public int MaxMissed { get; }

    public DeviceMemory(int maxMissed = 3) => MaxMissed = maxMissed;

    public bool HasDevices => known.Count > 0;

    /// <summary>Merges the latest scan into what is remembered and returns the list to show.</summary>
    public List<PlcDevice> Update(IEnumerable<PlcDevice> scan, DateTime now)
    {
        var seen = new HashSet<string>();
        foreach (var d in scan)
        {
            seen.Add(d.Mac);
            d.LastSeen = now; d.Missed = 0; d.LinksStale = false;
            if (known.TryGetValue(d.Mac, out var old))
            {
                // The adapter answered the hello but its link request got lost: keep the last known links.
                if (d.Links.Count == 0 && old.Links.Count > 0) { d.Links.AddRange(old.Links); d.LinksStale = true; }
                if (d.Firmware == "") d.Firmware = old.Firmware;
                if (d.Nid == "") d.Nid = old.Nid;
                if (!d.IsLocal && old.IsLocal && d.Links.Count == 0) d.IsLocal = true;
            }
            known[d.Mac] = d;
        }

        foreach (var mac in known.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            var d = known[mac];
            d.Missed++;
            if (d.Missed >= MaxMissed) known.Remove(mac);
        }

        return known.Values.OrderByDescending(d => d.IsLocal).ThenBy(d => d.Mac, StringComparer.Ordinal).ToList();
    }

    public void Clear() => known.Clear();
}
