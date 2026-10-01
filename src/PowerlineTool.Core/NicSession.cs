using System.Collections.Concurrent;
using SharpPcap;
using SharpPcap.LibPcap;

namespace PowerlineTool.Core;

/// <summary>Raw Layer-2 access to one Ethernet adapter through Npcap: HomePlug AV (0x88E1) and Broadcom (0x8912).</summary>
public sealed class NicSession : IDisposable
{
    public static readonly byte[] Oui = { 0x00, 0xB0, 0x52 };
    public static readonly byte[] Broadcast = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
    public static readonly byte[] LocalAlias = { 0x00, 0xB0, 0x52, 0x00, 0x00, 0x01 };

    public const ushort VS_SW_VER = 0xA000;
    public const ushort VS_NW_INFO = 0xA038;

    readonly LibPcapLiveDevice dev;
    readonly BlockingCollection<Mme> rx = new();
    public readonly byte[] MyMac;
    public readonly string Name;
    public int RawSeen;
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
            if (d.AsSpan(6, 6).SequenceEqual(MyMac)) return;
            RawSeen++;
            int off = d[14] == 0 ? 17 : 19; // MMV 0 has no FMI field
            ushort type = (ushort)(d[15] | d[16] << 8);
            rx.Add(new Mme(type, d[6..12], d[off..]));
        };
        dev.StartCapture();
    }

    // ---- HomePlug AV (Qualcomm) ------------------------------------------------------

    public byte[] BuildHomePlug(byte[] dst, ushort type, byte[] payload, byte mmv)
    {
        int hdr = mmv == 0 ? 17 : 19;
        var f = new byte[Math.Max(60, hdr + payload.Length)];
        dst.CopyTo(f, 0); MyMac.CopyTo(f, 6);
        f[12] = 0x88; f[13] = 0xE1;
        f[14] = mmv; f[15] = (byte)type; f[16] = (byte)(type >> 8);
        payload.CopyTo(f, hdr);
        return f;
    }

    /// <summary>Sends a vendor request and collects confirms (type + 1). Unicast returns on the first reply.</summary>
    public List<Mme> Request(byte[] dst, ushort type, int timeoutMs = 700, int retries = 3)
    {
        bool bcast = dst.SequenceEqual(Broadcast) || dst.SequenceEqual(LocalAlias);
        var result = new List<Mme>();
        for (int attempt = 0; attempt < retries && result.Count == 0; attempt++)
        {
            while (rx.TryTake(out _)) { }
            // Older (MMV 0) and newer (MMV 1) chipsets; discovery also goes to the local-device alias address.
            var targets = dst.SequenceEqual(Broadcast) ? new[] { Broadcast, LocalAlias } : new[] { dst };
            foreach (var tgt in targets)
                foreach (byte mmv in new byte[] { 1, 0 })
                    dev.SendPacket(BuildHomePlug(tgt, type, Oui, mmv));
            Log($"[{Name}] -> 0x{type:X4} to {MacUtil.Format(dst)} (attempt {attempt + 1}, HomePlug frames seen: {RawSeen})");
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (!rx.TryTake(out var m, 50) || m.Eth != 0x88E1) continue;
                if (m.Type != (ushort)(type + 1))
                {
                    Log($"[{Name}] (ignored) {MacUtil.Format(m.Src)} type 0x{m.Type:X4}");
                    continue;
                }
                if (!bcast && !m.Src.SequenceEqual(dst)) continue;
                if (result.Any(r => r.Src.SequenceEqual(m.Src))) continue;
                Log($"[{Name}] <- {MacUtil.Format(m.Src)} type 0x{m.Type:X4} : {Convert.ToHexString(m.Payload)}");
                result.Add(m);
                if (!bcast) break;
            }
        }
        return result;
    }

    // ---- Broadcom ----------------------------------------------------------------------

    /// <summary>Sends a Broadcom (0x8912) request and collects replies whose op equals <paramref name="expectOp"/>.</summary>
    public List<byte[]> BcmRequest(byte[] dst, byte[] body, byte expectOp, int? id, int timeoutMs = 800, int retries = 3, bool multi = false)
    {
        var result = new List<byte[]>();
        var frame = BcmProtocol.Frame(dst, MyMac, body);
        for (int attempt = 0; attempt < retries && result.Count == 0; attempt++)
        {
            while (rx.TryTake(out _)) { }
            dev.SendPacket(frame);
            Log($"[{Name}] -> BCM op 0x{body[1]:X2} to {MacUtil.Format(dst)} (attempt {attempt + 1})");
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (!rx.TryTake(out var m, 50) || m.Eth != 0x8912) continue;
                var r = m.Raw;
                if (r.Length < 24 || r[15] != expectOp) continue;
                if (id != null && r[22] != id) continue;
                if (!multi && !m.Src.SequenceEqual(dst)) continue;
                if (result.Any(x => x.AsSpan(6, 6).SequenceEqual(r.AsSpan(6, 6)))) continue;
                Log($"[{Name}] <- BCM {MacUtil.Format(m.Src)} op 0x{r[15]:X2}");
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
