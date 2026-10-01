using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using SharpPcap;
using SharpPcap.LibPcap;

namespace PowerlineTool.Core;

public record NicInfo(LibPcapLiveDevice Device, string Name, byte[] Mac);

public static class Scanner
{
    static readonly Regex Virtual = new("VirtualBox|VMware|Hyper-V|TAP-|Virtual|VPN|Loopback|Bluetooth", RegexOptions.IgnoreCase);

    /// <summary>Wired Ethernet adapters that are up. Wi-Fi, virtual and VPN adapters are skipped.</summary>
    public static List<NicInfo> ListNics()
    {
        var list = new List<NicInfo>();
        foreach (var d in CaptureDeviceList.Instance.OfType<LibPcapLiveDevice>())
        {
            var mac = d.Interface?.MacAddress?.GetAddressBytes();
            if (mac == null || mac.Length != 6) continue;
            var ni = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.GetPhysicalAddress().Equals(d.Interface.MacAddress));
            if (ni != null)
            {
                if (ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet) continue;
                if (Virtual.IsMatch(ni.Description)) continue;
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
            }
            list.Add(new NicInfo(d, d.Interface.FriendlyName ?? d.Description ?? d.Name, mac));
        }
        return list;
    }

    /// <summary>Broadcom first; Qualcomm only if nothing answered.</summary>
    public static List<PlcDevice> Scan(NicSession s)
    {
        var bcm = BcmProtocol.Scan(s);
        return bcm.Count > 0 ? bcm : QcaProtocol.Scan(s);
    }

    /// <summary>Scans every suitable adapter in parallel and merges devices seen on more than one.</summary>
    public static List<PlcDevice> ScanAll(Action<string> log)
    {
        var nics = ListNics();
        log($"Adapters: {string.Join(", ", nics.Select(n => n.Name))}");
        var all = new List<PlcDevice>();
        Exception npcapMissing = null;
        Task.WaitAll(nics.Select(n => Task.Run(() =>
        {
            try
            {
                using var s = new NicSession(n.Device, n.Name, n.Mac) { Log = log };
                var r = Scan(s);
                lock (all) all.AddRange(r);
            }
            catch (DllNotFoundException ex) { npcapMissing = ex; }
            catch (Exception ex) { log($"[{n.Name}] error: {ex.Message}"); }
        })).ToArray());
        if (npcapMissing != null) throw npcapMissing;
        return Merge(all);
    }

    public static List<PlcDevice> Merge(IEnumerable<PlcDevice> devices)
    {
        var map = new Dictionary<string, PlcDevice>();
        foreach (var d in devices)
        {
            if (!map.TryGetValue(d.Mac, out var have)) { map[d.Mac] = d; continue; }
            have.IsLocal |= d.IsLocal;
            if (have.Firmware == "") have.Firmware = d.Firmware;
            foreach (var l in d.Links.Where(l => have.Links.All(x => x.Peer != l.Peer))) have.Links.Add(l);
        }
        return map.Values.ToList();
    }
}

/// <summary>Writes every non-IP frame seen on all Ethernet adapters to a text file (for adding new adapter models).</summary>
public sealed class Sniffer : IDisposable
{
    readonly List<LibPcapLiveDevice> devs = new();
    readonly StreamWriter writer;
    readonly object sync = new();

    public Sniffer(string path, Action<string> log)
    {
        writer = new StreamWriter(path, false) { AutoFlush = true };
        foreach (var n in Scanner.ListNics())
        {
            var d = n.Device; var name = n.Name;
            d.Open(new DeviceConfiguration { Mode = DeviceModes.Promiscuous, ReadTimeout = 20 });
            try { d.Filter = "not ip and not ip6 and not arp and not stp"; } catch { }
            d.OnPacketArrival += (_, e) =>
            {
                var data = e.GetPacket().Data;
                if (data.Length < 14) return;
                lock (sync)
                    writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{name}] {MacUtil.Format(data[6..12])} -> {MacUtil.Format(data[0..6])} len={data.Length} : {Convert.ToHexString(data)}");
            };
            d.StartCapture(); devs.Add(d);
            log($"Capturing on: {name}");
        }
    }

    public void Dispose()
    {
        foreach (var d in devs) { try { d.StopCapture(); } catch { } try { d.Close(); } catch { } }
        lock (sync) writer.Dispose();
    }
}
