using System.Text;

namespace PowerlineTool.Core;

public record QcaStation(string Mac, int Tei, int Tx, int Rx);
public record QcaNetwork(string Nid, int Tei, int Role, int RecordSize, List<QcaStation> Stations);

/// <summary>
/// Qualcomm Atheros HomePlug AV adapters (EtherType 0x88E1, vendor OUI 00:B0:52), written from the public
/// open-plc-utils documentation. NOT yet tested on real hardware.
/// </summary>
public static class QcaProtocol
{
    /// <summary>OUI(3) status(1) deviceId(1) verLen(1) version(64).</summary>
    public static string ParseVersion(byte[] p)
    {
        if (p == null || p.Length < 6) return "";
        int len = Math.Min((int)p[5], p.Length - 6);
        return Encoding.ASCII.GetString(p, 6, len).TrimEnd('\0', ' ');
    }

    /// <summary>
    /// OUI(3) subver(1) reserved(1) numNetworks(1), then per network
    /// NID(7) SNID TEI ROLE CCO_MAC(6) CCO_TEI NSTA, followed by the station records.
    /// The station record size differs between chipset generations, so it is derived from the payload length.
    /// </summary>
    public static List<QcaNetwork> ParseNwInfo(byte[] p)
    {
        var nets = new List<QcaNetwork>();
        try
        {
            int o = 5;
            int count = p[o++];
            for (int n = 0; n < count; n++)
            {
                var nid = Convert.ToHexString(p, o, 7); o += 7;
                o += 1;                         // SNID
                int tei = p[o++];
                int role = p[o++];
                o += 6 + 1;                     // CCO MAC + TEI
                int nsta = p[o++];
                if (nsta == 0) continue;
                int size = (p.Length - o) / nsta;
                var net = new QcaNetwork(nid, tei, role, size, new List<QcaStation>());
                for (int i = 0; i < nsta; i++, o += size)
                {
                    int tx, rx;
                    if (size <= 16) { tx = p[o + 13]; rx = p[o + 14]; }
                    else if (size < 24) { tx = p[o + 16] | p[o + 17] << 8; rx = p[o + 18] | p[o + 19] << 8; }
                    else { tx = p[o + 16] | p[o + 17] << 8; rx = p[o + 20] | p[o + 21] << 8; }
                    net.Stations.Add(new QcaStation(MacUtil.Format(p[o..(o + 6)]), p[o + 6], tx, rx));
                }
                nets.Add(net);
            }
        }
        catch (IndexOutOfRangeException) { /* truncated payload: keep what was parsed */ }
        catch (ArgumentException) { }
        return nets;
    }

    static string RoleName(int role) => role == 2 ? "CCo" : role == 1 ? "PCo" : "Station";

    public static List<PlcDevice> Scan(NicSession s)
    {
        var found = new Dictionary<string, PlcDevice>();
        foreach (var local in s.Request(NicSession.Broadcast, NicSession.VS_SW_VER))
        {
            var lmac = MacUtil.Format(local.Src);
            var ldev = found[lmac] = new PlcDevice { Mac = lmac, IsLocal = true, Firmware = ParseVersion(local.Payload), Nic = s.Name };

            var nw = s.Request(local.Src, NicSession.VS_NW_INFO, 1200);
            if (nw.Count == 0) continue;
            foreach (var net in ParseNwInfo(nw[0].Payload))
            {
                s.Log($"[{s.Name}] NW_INFO: nid={net.Nid} stations={net.Stations.Count} record={net.RecordSize}B");
                ldev.Role = RoleName(net.Role);
                foreach (var st in net.Stations.Where(x => x.Mac != lmac))
                {
                    if (!found.TryGetValue(st.Mac, out var d)) found[st.Mac] = d = new PlcDevice { Mac = st.Mac, Nic = s.Name };
                    d.Tei = st.Tei; d.Nid = net.Nid;
                    ldev.Links.Add(new PlcLink(st.Mac, st.Tx, st.Rx));
                    d.Links.Add(new PlcLink(lmac, st.Rx, st.Tx));
                }
            }
        }

        foreach (var d in found.Values.Where(d => !d.IsLocal && d.Firmware == "").ToList())
        {
            var r = s.Request(MacUtil.Parse(d.Mac), NicSession.VS_SW_VER);
            if (r.Count > 0) d.Firmware = ParseVersion(r[0].Payload);
        }
        return found.Values.ToList();
    }
}
