namespace PowerlineTool.Core;

/// <summary>One direction-pair of link rates as reported by a device for one of its peers (Mbps).</summary>
public record PlcLink(string Peer, int Tx, int Rx)
{
    public int Min => Math.Min(Tx, Rx);
    public double Avg => (Tx + Rx) / 2.0;
}

public enum LinkQuality { Poor, Fair, Good, Excellent }

public static class Quality
{
    /// <summary>Thresholds follow tpPLC's legend (50 Mbps) with two extra steps for fast links.</summary>
    public static LinkQuality Of(int mbps) =>
        mbps >= 200 ? LinkQuality.Excellent : mbps >= 100 ? LinkQuality.Good : mbps >= 50 ? LinkQuality.Fair : LinkQuality.Poor;
}

public class PlcDevice
{
    public string Mac = "";
    public bool IsLocal;
    public string Role = "";
    public string Firmware = "";
    public string Nid = "";
    public int Tei;
    public string Nic = "";
    public List<PlcLink> Links = new();

    /// <summary>When a scan last saw this adapter answer.</summary>
    public DateTime LastSeen;
    /// <summary>Scans in a row that did not see this adapter (0 = seen in the latest scan).</summary>
    public int Missed;
    /// <summary>True when the links shown are from an earlier scan because the latest one did not return them.</summary>
    public bool LinksStale;
    public bool Stale => Missed > 0;
}

/// <summary>A received frame. <see cref="Raw"/> holds the whole Ethernet frame for EtherType 0x8912.</summary>
public record Mme(ushort Type, byte[] Src, byte[] Payload, ushort Eth = 0x88E1, byte[] Raw = null);

public static class MacUtil
{
    public static string Format(byte[] m) => string.Join(":", m.Select(b => b.ToString("X2")));

    public static byte[] Parse(string mac) => mac.Split(':').Select(h => Convert.ToByte(h, 16)).ToArray();

    /// <summary>Same naming scheme tpPLC uses for unnamed devices, e.g. Device_f5e2.</summary>
    public static string DefaultName(string mac)
    {
        var p = mac.Split(':');
        return p.Length < 2 ? mac : "Device_" + (p[^2] + p[^1]).ToLowerInvariant();
    }
}

/// <param name="DownLinks">Links the adapters list but report as 0/0: partner unplugged, in standby or out of reach.</param>
public record NetworkSummary(int Devices, int Links, double AvgMbps, string WeakestFrom, string WeakestTo, int WeakestMbps, int DownLinks = 0);

public static class Summary
{
    /// <summary>Counts each physical link once (both ends report it) and finds the weakest one.</summary>
    public static NetworkSummary Of(IReadOnlyCollection<PlcDevice> devices)
    {
        var pairs = new Dictionary<string, (string A, string B, PlcLink L)>();
        foreach (var d in devices)
            foreach (var l in d.Links)
            {
                var key = string.CompareOrdinal(d.Mac, l.Peer) < 0 ? d.Mac + "|" + l.Peer : l.Peer + "|" + d.Mac;
                if (!pairs.ContainsKey(key)) pairs[key] = (d.Mac, l.Peer, l);
            }
        if (pairs.Count == 0) return new NetworkSummary(devices.Count, 0, 0, "", "", 0);

        // A 0/0 reading means "no link", not "very slow", so it must not drag the average and the weakest link to 0.
        var up = pairs.Values.Where(p => p.L.Tx > 0 || p.L.Rx > 0).ToList();
        int down = pairs.Count - up.Count;
        if (up.Count == 0) return new NetworkSummary(devices.Count, pairs.Count, 0, "", "", 0, down);
        var weakest = up.OrderBy(p => p.L.Min).First();
        return new NetworkSummary(devices.Count, pairs.Count, up.Average(p => p.L.Avg), weakest.A, weakest.B, weakest.L.Min, down);
    }
}
