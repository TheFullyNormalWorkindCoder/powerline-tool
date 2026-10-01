using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using SharpPcap;
using SharpPcap.LibPcap;

namespace PowerlineTool;

public class PlcDevice
{
    public string Mac = "";
    public bool IsLocal;
    public string Role = "";
    public string Firmware = "";
    public int TxMbps = -1;
    public int RxMbps = -1;
    public string Nid = "";
    public int Tei;
    public string Nic = "";
    public string Links = "";
    public List<(string Peer, int Tx, int Rx)> LinkList = new();
}

public record Mme(ushort Type, byte[] Src, byte[] Payload, ushort Eth = 0x88E1, byte[] Raw = null);

/// <summary>HomePlug AV / Qualcomm Atheros vendor MMEs (EtherType 0x88E1) on one network adapter.</summary>
public sealed class NicSession : IDisposable
{
    public static readonly byte[] Oui = { 0x00, 0xB0, 0x52 };
    public static readonly byte[] Broadcast = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
    public static readonly byte[] LocalAlias = { 0x00, 0xB0, 0x52, 0x00, 0x00, 0x01 };
    public int RawSeen;

    public const ushort VS_SW_VER = 0xA000;
    public const ushort VS_NW_INFO = 0xA038;

    readonly LibPcapLiveDevice dev;
    readonly BlockingCollection<Mme> rx = new();
    public readonly byte[] MyMac;
    public readonly string Name;
    public Action<string> Log = _ => { };

    public NicSession(LibPcapLiveDevice dev, string name, byte[] mac)
    {
        this.dev = dev; Name = name; MyMac = mac;
        dev.Open(new DeviceConfiguration { Mode = DeviceModes.Promiscuous, ReadTimeout = 20 });
        try { dev.Filter = "ether proto 0x88e1 or ether proto 0x8912"; } catch { }
        dev.OnPacketArrival += (_, e) =>
        {
            var d = e.GetPacket().Data;
            if (d.Length < 17) return;
            if (d[12] == 0x89 && d[13] == 0x12) { rx.Add(new Mme(0, d[6..12], Array.Empty<byte>(), 0x8912, d)); return; }
            if (d[12] != 0x88 || d[13] != 0xE1) return;
            if (d[6] == MyMac[0] && d[7] == MyMac[1] && d[8] == MyMac[2] && d[9] == MyMac[3] && d[10] == MyMac[4] && d[11] == MyMac[5]) return;
            RawSeen++;
            int off = d[14] == 0 ? 17 : 19; // MMV 0 has no FMI field
            ushort type = (ushort)(d[15] | d[16] << 8);
            rx.Add(new Mme(type, d[6..12], d[off..]));
        };
        dev.StartCapture();
    }

    public static string Fmt(byte[] m) => string.Join(":", m.Select(b => b.ToString("X2")));

    byte[] Build(byte[] dst, ushort type, byte[] payload, byte mmv)
    {
        int hdr = mmv == 0 ? 17 : 19; // MMV 0 has no FMI field
        var f = new byte[Math.Max(60, hdr + payload.Length)];
        dst.CopyTo(f, 0); MyMac.CopyTo(f, 6);
        f[12] = 0x88; f[13] = 0xE1;
        f[14] = mmv; f[15] = (byte)type; f[16] = (byte)(type >> 8);
        payload.CopyTo(f, hdr);
        return f;
    }

    /// <summary>Sends a vendor request and collects confirms (type+1). Unicast returns on first reply.</summary>
    public List<Mme> Request(byte[] dst, ushort type, int timeoutMs = 700, int retries = 3)
    {
        bool bcast = dst.SequenceEqual(Broadcast) || dst.SequenceEqual(LocalAlias);
        var result = new List<Mme>();
        for (int attempt = 0; attempt < retries && result.Count == 0; attempt++)
        {
            while (rx.TryTake(out _)) { }
            // Older (MMV 0) and newer (MMV 1) chipsets; discovery additionally goes to the local-device alias address.
            var targets = dst.SequenceEqual(Broadcast) ? new[] { Broadcast, LocalAlias } : new[] { dst };
            foreach (var tgt in targets)
                foreach (byte mmv in new byte[] { 1, 0 })
                    dev.SendPacket(Build(tgt, type, Oui, mmv));
            Log($"[{Name}] -> 0x{type:X4} to {Fmt(dst)} (attempt {attempt + 1}, HomePlug frames seen so far: {RawSeen})");
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (!rx.TryTake(out var m, 50)) continue;
                if (m.Eth != 0x88E1) continue;
                if (m.Type != (ushort)(type + 1)) { Log($"[{Name}] (ignored) {Fmt(m.Src)} type 0x{m.Type:X4} : {Convert.ToHexString(m.Payload)}"); continue; }
                if (!bcast && !m.Src.SequenceEqual(dst)) continue;
                if (result.Any(r => r.Src.SequenceEqual(m.Src))) continue;
                Log($"[{Name}] <- {Fmt(m.Src)} type 0x{m.Type:X4} : {Convert.ToHexString(m.Payload)}");
                result.Add(m);
                if (!bcast) break;
            }
        }
        return result;
    }

    /// <summary>Broadcom (EtherType 0x8912) request. Collects frames whose op == expectOp (and id, if given).</summary>
    public List<byte[]> BcmRequest(byte[] dst, byte[] body, byte expectOp, int? id, int timeoutMs = 800, int retries = 3, bool multi = false)
    {
        var result = new List<byte[]>();
        var frame = new byte[Math.Max(60, 14 + body.Length)];
        dst.CopyTo(frame, 0); MyMac.CopyTo(frame, 6);
        frame[12] = 0x89; frame[13] = 0x12;
        body.CopyTo(frame, 14);
        for (int attempt = 0; attempt < retries && result.Count == 0; attempt++)
        {
            while (rx.TryTake(out _)) { }
            dev.SendPacket(frame);
            Log($"[{Name}] -> BCM op 0x{body[1]:X2} to {Fmt(dst)} (attempt {attempt + 1})");
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (!rx.TryTake(out var m, 50) || m.Eth != 0x8912) continue;
                var r = m.Raw;
                if (r[15] != expectOp) continue;
                if (id != null && r[22] != id) continue;
                if (!multi && !m.Src.SequenceEqual(dst)) continue;
                if (result.Any(x => x[6..12].SequenceEqual(r[6..12]))) continue;
                Log($"[{Name}] <- BCM {Fmt(m.Src)} op 0x{r[15]:X2} : {Convert.ToHexString(r, 14, Math.Min(r.Length - 14, 40))}");
                result.Add(r);
                if (!multi) break;
            }
        }
        return result;
    }

    public void Dispose()
    {
        try { dev.StopCapture(); } catch { }
        try { dev.Close(); } catch { }
    }
}

public static class PlcScanner
{
    /// <summary>Dumps every HomePlug (0x88E1) frame seen on all Ethernet adapters, both directions, to a text file.</summary>
    public static IDisposable StartSniff(string path, Action<string> log)
    {
        var writer = new StreamWriter(path, false) { AutoFlush = true };
        var devs = new List<LibPcapLiveDevice>();
        var sync = new object();
        foreach (var n in ListNics())
        {
            var d = n.dev; var nm = n.name;
            d.Open(new DeviceConfiguration { Mode = DeviceModes.Promiscuous, ReadTimeout = 20 });
            try { d.Filter = "not ip and not ip6 and not arp and not stp"; } catch { }
            d.OnPacketArrival += (_, e) =>
            {
                var raw = e.GetPacket();
                if (raw.Data.Length < 14) return;
                lock (sync) writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{nm}] {NicSession.Fmt(raw.Data[6..12])} -> {NicSession.Fmt(raw.Data[0..6])} len={raw.Data.Length} : {Convert.ToHexString(raw.Data)}");
            };
            d.StartCapture(); devs.Add(d);
            log($"Snimam na: {nm}");
        }
        return new SniffHandle(devs, writer);
    }

    sealed class SniffHandle : IDisposable
    {
        readonly List<LibPcapLiveDevice> devs; readonly StreamWriter w;
        public SniffHandle(List<LibPcapLiveDevice> d, StreamWriter w) { devs = d; this.w = w; }
        public void Dispose()
        {
            foreach (var d in devs) { try { d.StopCapture(); } catch { } try { d.Close(); } catch { } }
            w.Dispose();
        }
    }

    public static List<(LibPcapLiveDevice dev, string name, byte[] mac)> ListNics()
    {
        var list = new List<(LibPcapLiveDevice, string, byte[])>();
        foreach (var d in CaptureDeviceList.Instance.OfType<LibPcapLiveDevice>())
        {
            var mac = d.Interface?.MacAddress?.GetAddressBytes();
            if (mac == null || mac.Length != 6) continue;
            var ni = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.GetPhysicalAddress().Equals(d.Interface.MacAddress));
            if (ni != null && ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet) continue;
            if (ni != null && System.Text.RegularExpressions.Regex.IsMatch(ni.Description, "VirtualBox|VMware|Hyper-V|TAP-|Virtual|VPN|Loopback", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
            if (ni != null && ni.OperationalStatus != OperationalStatus.Up) continue;
            list.Add((d, d.Interface.FriendlyName ?? d.Description ?? d.Name, mac));
        }
        return list;
    }

    static string ParseVersion(byte[] p)
    {
        // OUI(3) status(1) deviceId(1) verLen(1) version(64)
        if (p.Length < 6) return "";
        int len = Math.Min(p[5], p.Length - 6);
        return System.Text.Encoding.ASCII.GetString(p, 6, len).TrimEnd('\0', ' ');
    }

    /// <summary>Scan one adapter: find locally attached adapter(s), then query the network for all stations.</summary>
    public static List<PlcDevice> Scan(NicSession s)
    {
        var bcm = ScanBcm(s);
        return bcm.Count > 0 ? bcm : ScanQca(s);
    }

    static readonly byte[] BcmHdr = { 0xA0, 0x00, 0x00, 0x00, 0x1F, 0x84 };
    static readonly byte[] BcmHello = Convert.FromHexString("0170A00000001F8401A397A25553BEF1FCF9796B521413E9E2000F94CA6F980D6101000060018803FA00FCFA22");
    static readonly Random Rng = new();

    static byte[] BcmBody(byte op, byte id, params byte[] args)
    {
        var b = new List<byte> { 0x02, op };
        b.AddRange(BcmHdr); b.Add(id); b.AddRange(args);
        return b.ToArray();
    }

    /// <summary>Broadcom BCM60xxx adapters (e.g. TL-PA7017): discovery by broadcast, then NID + station rates per device.</summary>
    static List<PlcDevice> ScanBcm(NicSession s)
    {
        var list = new List<PlcDevice>();
        var hello = s.BcmRequest(NicSession.Broadcast, BcmHello, 0x71, null, 1200, 3, multi: true);
        foreach (var r in hello.OrderBy(r => r[23]))
        {
            int len = r[24];
            var fw = System.Text.Encoding.ASCII.GetString(r, 25, Math.Min(len, r.Length - 25)).TrimEnd('\0');
            list.Add(new PlcDevice { Mac = NicSession.Fmt(r[6..12]), Firmware = fw, Tei = r[23], IsLocal = r[23] == 1, Nic = s.Name });
        }

        foreach (var d in list)
        {
            var mac = d.Mac.Split(':').Select(h => Convert.ToByte(h, 16)).ToArray();
            byte id = (byte)Rng.Next(1, 255);
            var nidResp = s.BcmRequest(mac, BcmBody(0x5C, id, 0x23), 0x5D, id);
            if (nidResp.Count == 0 || nidResp[0][23] != 1) continue;
            int nlen = nidResp[0][24] | nidResp[0][25] << 8;
            if (nlen != 7) continue;
            var nid = nidResp[0][26..33];
            d.Nid = Convert.ToHexString(nid);

            id = (byte)Rng.Next(1, 255);
            var args = new byte[] { 0x01 }.Concat(nid).ToArray();
            var st = s.BcmRequest(mac, BcmBody(0x2C, id, args), 0x2D, id);
            if (st.Count == 0) continue;
            var r = st[0];
            int count = r[23];
            var links = new List<string>();
            for (int i = 0; i < count && 24 + (i + 1) * 10 <= r.Length; i++)
            {
                int o = 24 + i * 10;
                var peer = NicSession.Fmt(r[o..(o + 6)]);
                int tx = (r[o + 6] | r[o + 7] << 8) & 0x3FFF;
                int rxr = (r[o + 8] | r[o + 9] << 8) & 0x3FFF;
                if (i == 0) { d.TxMbps = tx; d.RxMbps = rxr; }
                links.Add($"{peer[9..]}: TX {tx} / RX {rxr}");
                d.LinkList.Add((peer, tx, rxr));
            }
            d.Links = string.Join("; ", links);
        }
        return list;
    }

    static List<PlcDevice> ScanQca(NicSession s)
    {
        var found = new Dictionary<string, PlcDevice>();
        foreach (var local in s.Request(NicSession.Broadcast, NicSession.VS_SW_VER))
        {
            var mac = NicSession.Fmt(local.Src);
            found[mac] = new PlcDevice { Mac = mac, IsLocal = true, Firmware = ParseVersion(local.Payload), Nic = s.Name };

            var nw = s.Request(local.Src, NicSession.VS_NW_INFO, 1200);
            if (nw.Count == 0) continue;
            ParseNwInfo(nw[0].Payload, found, mac, s);
        }

        foreach (var d in found.Values.Where(d => !d.IsLocal && d.Firmware == "").ToList())
        {
            var mac = d.Mac.Split(':').Select(h => Convert.ToByte(h, 16)).ToArray();
            var r = s.Request(mac, NicSession.VS_SW_VER);
            if (r.Count > 0) d.Firmware = ParseVersion(r[0].Payload);
        }
        return found.Values.ToList();
    }

    static void ParseNwInfo(byte[] p, Dictionary<string, PlcDevice> found, string localMac, NicSession s)
    {
        try
        {
            // OUI(3) subver(1) reserved(1) numNetworks(1) | per network: NID(7) SNID TEI ROLE CCO_MAC(6) CCO_TEI NSTA | stations
            int o = 5;
            int nets = p[o++];
            for (int n = 0; n < nets; n++)
            {
                var nid = Convert.ToHexString(p, o, 7); o += 7;
                o += 1;                // SNID
                int tei = p[o++];
                int role = p[o++];
                o += 6 + 1;            // CCO MAC + TEI
                int nsta = p[o++];
                if (nsta == 0) continue;
                int size = (p.Length - o) / nsta; // station record size differs between chipset generations
                s.Log($"[{s.Name}] NW_INFO: nid={nid} stations={nsta} record={size}B");

                for (int i = 0; i < nsta; i++, o += size)
                {
                    var mac = NicSession.Fmt(p[o..(o + 6)]);
                    int tx, rxr;
                    if (size <= 16) { tx = p[o + 13]; rxr = p[o + 14]; }
                    else if (size < 24) { tx = p[o + 16] | p[o + 17] << 8; rxr = p[o + 18] | p[o + 19] << 8; }
                    else { tx = p[o + 16] | p[o + 17] << 8; rxr = p[o + 20] | p[o + 21] << 8; }

                    if (!found.TryGetValue(mac, out var d))
                        found[mac] = d = new PlcDevice { Mac = mac, Nic = s.Name };
                    d.Tei = p[o + 6]; d.TxMbps = tx; d.RxMbps = rxr; d.Nid = nid;
                }
                if (found.TryGetValue(localMac, out var l)) { l.Role = role == 2 ? "CCo" : role == 1 ? "PCo" : "Station"; _ = tei; }
            }
        }
        catch (Exception ex) { s.Log($"[{s.Name}] NW_INFO parse failed: {ex.Message}"); }
    }
}







