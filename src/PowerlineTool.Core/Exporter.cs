using System.Text;
using System.Text.Json;

namespace PowerlineTool.Core;

/// <summary>Plain-text exports of the current device list.</summary>
public static class Exporter
{
    static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    public static string Csv(IEnumerable<PlcDevice> devices, Settings settings)
    {
        var sb = new StringBuilder("Type,Name,MAC,Link to,TX,RX,Quality,Firmware\r\n");
        foreach (var d in devices)
        {
            var type = d.IsLocal ? "Local" : "Remote";
            if (d.Links.Count == 0) sb.Append($"{type},{Q(settings.NameOf(d.Mac))},{d.Mac},,,,,{Q(d.Firmware)}\r\n");
            foreach (var l in d.Links)
                sb.Append($"{type},{Q(settings.NameOf(d.Mac))},{d.Mac},{l.Peer},{l.Tx},{l.Rx},{Quality.Of(l.Min)},{Q(d.Firmware)}\r\n");
        }
        return sb.ToString();
    }

    public static string Json(IReadOnlyCollection<PlcDevice> devices, Settings settings)
    {
        var s = Summary.Of(devices);
        var doc = new
        {
            generated = DateTime.Now,
            summary = new { s.Devices, s.Links, avgMbps = Math.Round(s.AvgMbps, 1), weakestMbps = s.WeakestMbps },
            devices = devices.Select(d => new
            {
                name = settings.NameOf(d.Mac), mac = d.Mac, local = d.IsLocal, firmware = d.Firmware, nid = d.Nid, nic = d.Nic,
                links = d.Links.Select(l => new { peer = l.Peer, peerName = settings.NameOf(l.Peer), tx = l.Tx, rx = l.Rx, quality = Quality.Of(l.Min).ToString() }),
            }),
        };
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
}
