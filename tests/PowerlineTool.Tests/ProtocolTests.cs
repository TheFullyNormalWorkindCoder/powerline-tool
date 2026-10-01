using System.Text;
using PowerlineTool.Core;
using Xunit;

namespace PowerlineTool.Tests;

public class BcmProtocolTests
{
    static readonly byte[] Dst = { 1, 2, 3, 4, 5, 6 };
    static readonly byte[] Src = { 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6 };

    /// <summary>A reply frame as an adapter would send it (op byte already the reply op).</summary>
    static byte[] Reply(byte op, byte id, params byte[] args) => BcmProtocol.Frame(Dst, Src, BcmProtocol.Body(op, id, args));

    [Fact]
    public void Body_matches_the_bytes_seen_in_the_capture()
    {
        // 02 5C | A0 00 00 00 1F 84 | id 29 | var 24
        Assert.Equal(Convert.FromHexString("025CA00000001F842924"), BcmProtocol.Body(0x5C, 0x29, 0x24));
    }

    [Fact]
    public void Hello_is_the_captured_45_byte_request()
    {
        Assert.Equal(45, BcmProtocol.Hello.Length);
        Assert.Equal(0x01, BcmProtocol.Hello[0]);
        Assert.Equal(BcmProtocol.OpHello, BcmProtocol.Hello[1]);
    }

    [Fact]
    public void Frame_is_padded_to_60_bytes_and_carries_the_ethertype()
    {
        var f = BcmProtocol.Frame(Dst, Src, BcmProtocol.Body(0x5C, 1, 0x23));
        Assert.Equal(60, f.Length);
        Assert.Equal(new byte[] { 0x89, 0x12 }, f[12..14]);
        Assert.Equal(Dst, f[0..6]);
        Assert.Equal(Src, f[6..12]);
    }

    [Theory]
    [InlineData(0x40D4, 212)]   // value seen in the capture; tpPLC showed about 210
    [InlineData(0x4092, 146)]
    [InlineData(0x0000, 0)]
    [InlineData(0x3FFF, 16383)]
    public void Rate_keeps_the_low_14_bits(int raw, int mbps) => Assert.Equal(mbps, BcmProtocol.Rate(raw));

    [Fact]
    public void ParseRates_decodes_a_station_entry()
    {
        var raw = Reply(0x2D, 0x11, 1, 0xAA, 0xBB, 0xCC, 0, 0, 2, 0xD4, 0x40, 0x92, 0x40);
        var link = Assert.Single(BcmProtocol.ParseRates(raw));
        Assert.Equal("AA:BB:CC:00:00:02", link.Peer);
        Assert.Equal(212, link.Tx);
        Assert.Equal(146, link.Rx);
    }

    [Fact]
    public void ParseRates_reads_several_entries()
    {
        var raw = Reply(0x2D, 0x11, 2,
            0xAA, 0xBB, 0xCC, 0, 0, 2, 100, 0, 50, 0,
            0xAA, 0xBB, 0xCC, 0, 0, 3, 10, 0, 20, 0);
        var links = BcmProtocol.ParseRates(raw);
        Assert.Equal(2, links.Count);
        Assert.Equal(20, links[1].Rx);
    }

    [Fact]
    public void ParseRates_ignores_an_entry_the_frame_is_too_short_for()
    {
        // claims two entries but the frame ends after the first one's padding-free end
        var body = BcmProtocol.Body(0x2D, 0x11, 2, 0xAA, 0xBB, 0xCC, 0, 0, 2, 100, 0, 50, 0);
        var raw = new byte[14 + body.Length]; // no padding, so the second entry does not fit
        Dst.CopyTo(raw, 0); Src.CopyTo(raw, 6); raw[12] = 0x89; raw[13] = 0x12; body.CopyTo(raw, 14);
        Assert.Single(BcmProtocol.ParseRates(raw));
    }

    [Fact]
    public void ParseRates_returns_nothing_for_another_op_or_null()
    {
        Assert.Empty(BcmProtocol.ParseRates(Reply(0x5D, 1, 1)));
        Assert.Empty(BcmProtocol.ParseRates(null));
    }

    [Fact]
    public void TryParseHello_reads_index_and_firmware()
    {
        var fw = "tpver_701E14_190426_901";
        var args = new byte[] { 1, (byte)fw.Length }.Concat(Encoding.ASCII.GetBytes(fw)).ToArray();
        Assert.True(BcmProtocol.TryParseHello(Reply(0x71, 1, args), out var index, out var firmware));
        Assert.Equal(1, index);
        Assert.Equal(fw, firmware);
    }

    [Fact]
    public void TryParseHello_rejects_other_ops()
    {
        Assert.False(BcmProtocol.TryParseHello(Reply(0x5D, 1, 1, 0), out _, out _));
        Assert.False(BcmProtocol.TryParseHello(null, out _, out _));
    }

    [Fact]
    public void TryParseVar_returns_the_data_block()
    {
        var nid = new byte[] { 1, 2, 3, 4, 5, 6, 7 };
        var args = new byte[] { 0x01, 7, 0 }.Concat(nid).ToArray();
        Assert.True(BcmProtocol.TryParseVar(Reply(0x5D, 9, args), out var data));
        Assert.Equal(nid, data);
    }

    [Fact]
    public void TryParseVar_rejects_a_non_ok_status_and_a_lying_length()
    {
        Assert.False(BcmProtocol.TryParseVar(Reply(0x5D, 9, 0x04, 1, 0, 0xAA), out _));
        Assert.False(BcmProtocol.TryParseVar(Reply(0x5D, 9, 0x01, 200, 0), out _)); // length 200 does not fit
    }
}

public class QcaProtocolTests
{
    [Fact]
    public void ParseVersion_reads_the_length_prefixed_string()
    {
        var p = new byte[] { 0x00, 0xB0, 0x52, 0, 1, 5 }.Concat(Encoding.ASCII.GetBytes("1.2.3")).Concat(new byte[10]).ToArray();
        Assert.Equal("1.2.3", QcaProtocol.ParseVersion(p));
    }

    [Fact]
    public void ParseVersion_survives_short_input() => Assert.Equal("", QcaProtocol.ParseVersion(new byte[] { 1, 2 }));

    static byte[] NwInfo(int recordSize, params (byte[] mac, int tx, int rx)[] stations)
    {
        var p = new List<byte> { 0x00, 0xB0, 0x52, 1, 0, 1 };           // OUI, subver, reserved, 1 network
        p.AddRange(new byte[] { 1, 2, 3, 4, 5, 6, 7 });                   // NID
        p.AddRange(new byte[] { 0, 1, 2 });                               // SNID, TEI, ROLE=CCo
        p.AddRange(new byte[] { 9, 9, 9, 9, 9, 9, 1 });                   // CCO MAC + TEI
        p.Add((byte)stations.Length);
        foreach (var (mac, tx, rx) in stations)
        {
            var r = new byte[recordSize];
            mac.CopyTo(r, 0); r[6] = 5;
            if (recordSize <= 16) { r[13] = (byte)tx; r[14] = (byte)rx; }
            else if (recordSize < 24) { r[16] = (byte)tx; r[17] = (byte)(tx >> 8); r[18] = (byte)rx; r[19] = (byte)(rx >> 8); }
            else { r[16] = (byte)tx; r[17] = (byte)(tx >> 8); r[20] = (byte)rx; r[21] = (byte)(rx >> 8); }
            p.AddRange(r);
        }
        return p.ToArray();
    }

    [Theory]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(24)]
    public void ParseNwInfo_handles_the_known_record_sizes(int size)
    {
        var a = new byte[] { 1, 1, 1, 1, 1, 1 }; var b = new byte[] { 2, 2, 2, 2, 2, 2 };
        var nets = QcaProtocol.ParseNwInfo(NwInfo(size, (a, 150, 90), (b, 60, 40)));
        var net = Assert.Single(nets);
        Assert.Equal(size, net.RecordSize);
        Assert.Equal(2, net.Role);
        Assert.Equal(2, net.Stations.Count);
        Assert.Equal("01:01:01:01:01:01", net.Stations[0].Mac);
        Assert.Equal(150, net.Stations[0].Tx);
        Assert.Equal(40, net.Stations[1].Rx);
    }

    [Fact]
    public void ParseNwInfo_never_throws_on_garbage()
    {
        Assert.Empty(QcaProtocol.ParseNwInfo(new byte[] { 1, 2, 3 }));
        var truncated = NwInfo(20, (new byte[6], 1, 1))[..30];
        QcaProtocol.ParseNwInfo(truncated); // must simply not throw
    }
}
