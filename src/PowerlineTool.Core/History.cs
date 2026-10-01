using System.Globalization;
using System.Text;

namespace PowerlineTool.Core;

public record Sample(DateTime Time, int Tx, int Rx);

/// <summary>In-memory rate history per link. Samples are stored from the point of view of the lower MAC.</summary>
public class HistoryStore
{
    readonly Dictionary<string, List<Sample>> data = new();
    readonly int capacity;

    public HistoryStore(int capacity = 1440) => this.capacity = capacity;

    static bool Ordered(string a, string b) => string.CompareOrdinal(a, b) <= 0;
    static string Key(string a, string b) => Ordered(a, b) ? a + "|" + b : b + "|" + a;

    public void Add(DateTime time, IEnumerable<PlcDevice> devices)
    {
        var tick = new Dictionary<string, (int Tx, int Rx, bool Authoritative)>();
        foreach (var d in devices)
            foreach (var l in d.Links)
            {
                bool lower = Ordered(d.Mac, l.Peer);
                var (tx, rx) = lower ? (l.Tx, l.Rx) : (l.Rx, l.Tx);
                var key = Key(d.Mac, l.Peer);
                if (!tick.TryGetValue(key, out var have) || (lower && !have.Authoritative))
                    tick[key] = (tx, rx, lower);
            }
        foreach (var (key, v) in tick)
        {
            if (!data.TryGetValue(key, out var list)) data[key] = list = new List<Sample>();
            list.Add(new Sample(time, v.Tx, v.Rx));
            if (list.Count > capacity) list.RemoveRange(0, list.Count - capacity);
        }
    }

    public IEnumerable<(string A, string B)> Pairs =>
        data.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => { var p = k.Split('|'); return (p[0], p[1]); });

    /// <summary>Samples oriented so that Tx means "from <paramref name="a"/> to <paramref name="b"/>".</summary>
    public IReadOnlyList<Sample> Series(string a, string b)
    {
        if (!data.TryGetValue(Key(a, b), out var list)) return Array.Empty<Sample>();
        return Ordered(a, b) ? list : list.Select(s => s with { Tx = s.Rx, Rx = s.Tx }).ToList();
    }

    public void Clear() => data.Clear();
}

public static class HistoryCsv
{
    public const string Header = "time,from,to,tx_mbps,rx_mbps";

    public static string Lines(DateTime time, IEnumerable<PlcDevice> devices)
    {
        var sb = new StringBuilder();
        foreach (var d in devices)
            foreach (var l in d.Links)
                sb.Append(time.ToString("s", CultureInfo.InvariantCulture)).Append(',').Append(d.Mac).Append(',')
                  .Append(l.Peer).Append(',').Append(l.Tx).Append(',').Append(l.Rx).Append('\n');
        return sb.ToString();
    }

    public static void Append(string path, DateTime time, IEnumerable<PlcDevice> devices)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path)) File.WriteAllText(path, Header + "\n");
        File.AppendAllText(path, Lines(time, devices));
    }
}
