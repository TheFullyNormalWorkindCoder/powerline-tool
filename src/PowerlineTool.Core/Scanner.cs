using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using SharpPcap;
using SharpPcap.LibPcap;

namespace PowerlineTool.Core;

public record NicInfo(LibPcapLiveDevice Device, string Name, byte[] Mac);

/// <summary>Outcome of one scan: what was found, and enough context to explain an empty result.</summary>
public class ScanResult
{
    public List<PlcDevice> Devices { get; set; } = new();
    /// <summary>Wired adapters that were usable for this scan.</summary>
    public List<string> Nics { get; set; } = new();
    /// <summary>Why adapters were skipped, e.g. "Ethernet 2: virtual adapter".</summary>
    public List<string> Skipped { get; set; } = new();
    /// <summary>Machine-readable problems: npcap_missing, no_nic, no_reply, tpplc_running.</summary>
    public List<string> Warnings { get; set; } = new();
    public int ElapsedMs { get; set; }
}

public static class Scanner
{
    static readonly Regex Virtual = new("VirtualBox|VMware|Hyper-V|TAP-|Virtual|VPN|Loopback|Bluetooth", RegexOptions.IgnoreCase);

    /// <summary>
    /// Wired Ethernet adapters that are up. Wi-Fi, virtual and VPN adapters are skipped.
    /// A fresh device list is requested every time: the cached <c>CaptureDeviceList.Instance</c> hands out the same
    /// device objects again and again, which piles up event handlers and re-opens already used handles.
    /// </summary>
    public static List<NicInfo> ListNics(List<string> skipped = null)
    {
        var list = new List<NicInfo>();
        var all = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var d in CaptureDeviceList.New().OfType<LibPcapLiveDevice>())
        {
            var name = d.Interface?.FriendlyName ?? d.Description ?? d.Name;
            var mac = d.Interface?.MacAddress?.GetAddressBytes();
            if (mac == null || mac.Length != 6) continue;
            // Several Windows interfaces can share a MAC (bridges, Wi-Fi Direct); prefer the one that qualifies.
            var candidates = all.Where(n => n.GetPhysicalAddress().Equals(d.Interface.MacAddress)).ToList();
            if (candidates.Count > 0)
            {
                var ni = candidates.FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet && !Virtual.IsMatch(n.Description))
                         ?? candidates[0];
                string why = null;
                if (ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet) why = "not wired Ethernet";
                else if (Virtual.IsMatch(ni.Description)) why = "virtual adapter";
                else if (ni.OperationalStatus != OperationalStatus.Up) why = "link is " + ni.OperationalStatus.ToString().ToLowerInvariant();
                if (why != null) { skipped?.Add($"{name}: {why}"); continue; }
            }
            list.Add(new NicInfo(d, name, mac));
        }
        return list;
    }

    /// <summary>Programs that talk to the same adapters and can get in each other's way.</summary>
    public static bool OtherPlcToolRunning()
    {
        try
        {
            return System.Diagnostics.Process.GetProcessesByName("plcu").Length > 0
                || System.Diagnostics.Process.GetProcessesByName("tpPLC").Length > 0;
        }
        catch { return false; }
    }

    /// <summary>Broadcom first; Qualcomm only if nothing answered.</summary>
    public static List<PlcDevice> Scan(NicSession s)
    {
        var bcm = BcmProtocol.Scan(s);
        return bcm.Count > 0 ? bcm : QcaProtocol.Scan(s);
    }

    /// <summary>Scans every suitable adapter in parallel and merges devices seen on more than one.</summary>
    public static List<PlcDevice> ScanAll(Action<string> log) => ScanAllDetailed(log).Devices;

    /// <summary>Like <see cref="ScanAll"/>, but also reports which adapters were used or skipped and what looks wrong.</summary>
    public static ScanResult ScanAllDetailed(Action<string> log)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new ScanResult();
        var nics = ListNics(result.Skipped);
        result.Nics = nics.Select(n => n.Name).ToList();
        log($"Adapters: {(nics.Count == 0 ? "(none)" : string.Join(", ", result.Nics))}");
        foreach (var s in result.Skipped) log($"Skipped {s}");

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

        result.Devices = Merge(all);
        if (nics.Count == 0) result.Warnings.Add("no_nic");
        else if (result.Devices.Count == 0) result.Warnings.Add("no_reply");
        if (OtherPlcToolRunning()) result.Warnings.Add("tpplc_running");
        result.ElapsedMs = (int)sw.ElapsedMilliseconds;
        return result;
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
