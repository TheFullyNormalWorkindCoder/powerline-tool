using System.Text;

namespace PowerlineTool.Core;

/// <summary>
/// Broadcom BCM60xxx powerline adapters (EtherType 0x8912), e.g. TP-Link TL-PA7017.
/// Reverse-engineered from captured traffic, read-only requests only. See docs/PROTOCOL.md.
/// </summary>
public static class BcmProtocol
{
    public const ushort EtherType = 0x8912;
    public const byte OpHello = 0x70, OpGetVar = 0x5C, OpRates = 0x2C;
    public const byte VarNid = 0x23;

    static readonly byte[] Header = { 0xA0, 0x00, 0x00, 0x00, 0x1F, 0x84 };
    public static readonly byte[] Hello = Convert.FromHexString(
        "0170A00000001F8401A397A25553BEF1FCF9796B521413E9E2000F94CA6F980D6101000060018803FA00FCFA22");
    static readonly Random Rng = new();

    /// <summary>Body of a unicast request: version, op, constant header, request id, args.</summary>
    public static byte[] Body(byte op, byte id, params byte[] args)
    {
        var b = new List<byte> { 0x02, op };
        b.AddRange(Header); b.Add(id); b.AddRange(args);
        return b.ToArray();
    }

    /// <summary>Complete Ethernet frame, zero padded to the 60-byte minimum.</summary>
    public static byte[] Frame(byte[] dst, byte[] src, byte[] body)
    {
        var f = new byte[Math.Max(60, 14 + body.Length)];
        dst.CopyTo(f, 0); src.CopyTo(f, 6);
        f[12] = 0x89; f[13] = 0x12;
        body.CopyTo(f, 14);
        return f;
    }

    /// <summary>The adapter reports the low 14 bits; the top bits look like flags (0x40D4 = 212 Mbps).</summary>
    public static int Rate(int raw) => raw & 0x3FFF;

    public static bool TryParseHello(byte[] r, out int index, out string firmware)
    {
        index = 0; firmware = "";
        if (r == null || r.Length < 26 || r[15] != OpHello + 1) return false;
        index = r[23];
        int len = Math.Min((int)r[24], r.Length - 25);
        firmware = Encoding.ASCII.GetString(r, 25, len).TrimEnd('\0');
        return true;
    }

    public static bool TryParseVar(byte[] r, out byte[] data)
    {
        data = null;
        if (r == null || r.Length < 26 || r[15] != OpGetVar + 1 || r[23] != 1) return false;
        int len = r[24] | r[25] << 8;
        if (26 + len > r.Length) return false;
        data = r[26..(26 + len)];
        return true;
    }

    public static List<PlcLink> ParseRates(byte[] r)
    {
        var list = new List<PlcLink>();
        if (r == null || r.Length < 24 || r[15] != OpRates + 1) return list;
        int count = r[23];
        for (int i = 0; i < count && 24 + (i + 1) * 10 <= r.Length; i++)
        {
            int o = 24 + i * 10;
            list.Add(new PlcLink(MacUtil.Format(r[o..(o + 6)]),
                Rate(r[o + 6] | r[o + 7] << 8), Rate(r[o + 8] | r[o + 9] << 8)));
        }
        return list;
    }

    /// <summary>Broadcast hello finds every adapter (local and remote); then NID and link rates per adapter.</summary>
    public static List<PlcDevice> Scan(NicSession s)
    {
        var list = new List<PlcDevice>();
        foreach (var r in s.BcmRequest(NicSession.Broadcast, Hello, OpHello + 1, null, 1200, 3, multi: true).OrderBy(r => r[23]))
        {
            if (!TryParseHello(r, out var index, out var fw)) continue;
            // Index 1 is the adapter plugged into this PC in every capture so far (heuristic, see docs).
            list.Add(new PlcDevice { Mac = MacUtil.Format(r[6..12]), Firmware = fw, Tei = index, IsLocal = index == 1, Nic = s.Name });
        }

        foreach (var d in list)
        {
            var mac = MacUtil.Parse(d.Mac);
            byte id = (byte)Rng.Next(1, 255);
            var nidResp = s.BcmRequest(mac, Body(OpGetVar, id, VarNid), OpGetVar + 1, id);
            if (nidResp.Count == 0 || !TryParseVar(nidResp[0], out var nid) || nid.Length != 7) continue;
            d.Nid = Convert.ToHexString(nid);

            id = (byte)Rng.Next(1, 255);
            var args = new byte[] { 0x01 }.Concat(nid).ToArray();
            var st = s.BcmRequest(mac, Body(OpRates, id, args), OpRates + 1, id);
            if (st.Count > 0) d.Links.AddRange(ParseRates(st[0]));
        }
        return list;
    }
}
