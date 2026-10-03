using System.Text.Json;
using PowerlineTool.Core;
using Xunit;

namespace PowerlineTool.Tests;

public class UiStateTests
{
    static async Task<ScanCoordinator> Demo(int scans = 3)
    {
        int tick = 0;
        var c = new ScanCoordinator(new Settings(), () => new ScanResult { Devices = DemoData.Devices(tick++), Nics = new List<string> { "Ethernet" } });
        for (int i = 0; i < scans; i++) await c.ScanAsync();
        return c;
    }

    static UiFlags Flags(DateTime? next = null, bool npcap = false) => new("1.2.0", true, next, false, npcap, false);

    [Fact]
    public async Task Json_is_camelCase_and_contains_what_the_ui_reads()
    {
        var c = await Demo();
        using var doc = JsonDocument.Parse(UiState.Serialize(c, Flags(DateTime.Now.AddSeconds(5))));
        var root = doc.RootElement;
        Assert.Equal("state", root.GetProperty("type").GetString());
        var st = root.GetProperty("state");

        Assert.Equal("1.2.0", st.GetProperty("app").GetProperty("version").GetString());
        Assert.False(st.GetProperty("scanning").GetBoolean());
        Assert.True(st.GetProperty("lastScan").GetInt64() > 1_700_000_000_000);
        Assert.True(st.GetProperty("nextScanAt").GetInt64() > st.GetProperty("lastScan").GetInt64());

        var devices = st.GetProperty("devices");
        Assert.Equal(3, devices.GetArrayLength());
        var first = devices[0];
        Assert.True(first.GetProperty("local").GetBoolean());
        Assert.Equal("Device_0001", first.GetProperty("name").GetString());
        var link = first.GetProperty("links")[0];
        Assert.Equal("Device_0002", link.GetProperty("peerName").GetString());
        Assert.Contains(link.GetProperty("quality").GetString(), new[] { "poor", "fair", "good", "excellent" });

        var settings = st.GetProperty("settings");
        Assert.Equal("system", settings.GetProperty("theme").GetString());
        Assert.Equal(10, settings.GetProperty("intervalSec").GetInt32());
        Assert.Equal("web", settings.GetProperty("ui").GetString());
    }

    [Fact]
    public async Task History_is_keyed_by_the_sorted_pair_and_holds_ms_tx_rx_triples()
    {
        var c = await Demo(4);
        using var doc = JsonDocument.Parse(UiState.Serialize(c, Flags()));
        var hist = doc.RootElement.GetProperty("state").GetProperty("history");
        var key = "AA:BB:CC:00:00:01|AA:BB:CC:00:00:02";
        var samples = hist.GetProperty(key);
        Assert.Equal(4, samples.GetArrayLength());
        Assert.Equal(3, samples[0].GetArrayLength());
        Assert.True(samples[0][0].GetInt64() > 1_700_000_000_000);
    }

    [Fact]
    public async Task History_sent_to_the_ui_is_capped()
    {
        var c = await Demo(1);
        var t = DateTime.Now;
        for (int i = 0; i < UiState.HistoryPerLink + 50; i++) c.History.Add(t.AddSeconds(i), DemoData.Devices(i));
        using var doc = JsonDocument.Parse(UiState.Serialize(c, Flags()));
        var arr = doc.RootElement.GetProperty("state").GetProperty("history").GetProperty("AA:BB:CC:00:00:01|AA:BB:CC:00:00:02");
        Assert.Equal(UiState.HistoryPerLink, arr.GetArrayLength());
    }

    [Fact]
    public async Task Npcap_warning_is_added_and_nulls_are_explicit()
    {
        var c = new ScanCoordinator(new Settings(), () => new ScanResult());
        using var doc = JsonDocument.Parse(UiState.Serialize(c, Flags(npcap: true)));
        var st = doc.RootElement.GetProperty("state");
        Assert.Contains("npcap_missing", st.GetProperty("warnings").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(JsonValueKind.Null, st.GetProperty("lastScan").ValueKind);
        Assert.Equal(JsonValueKind.Null, st.GetProperty("nextScanAt").ValueKind);
        Assert.Equal(0, st.GetProperty("devices").GetArrayLength());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Stale_and_events_reach_the_ui()
    {
        var script = new Queue<ScanResult>(new[] { new ScanResult { Devices = DemoData.Devices(0) }, new ScanResult { Devices = DemoData.Devices(0).Take(1).ToList() } });
        var c = new ScanCoordinator(new Settings(), () => script.Count > 1 ? script.Dequeue() : script.Peek()) { RetryDelayMs = 1 };
        await c.ScanAsync();
        await c.ScanAsync();
        using var doc = JsonDocument.Parse(UiState.Serialize(c, Flags()));
        var devices = doc.RootElement.GetProperty("state").GetProperty("devices");
        Assert.Contains(devices.EnumerateArray(), d => d.GetProperty("stale").GetBoolean() && d.GetProperty("missed").GetInt32() == 1);
    }
}
