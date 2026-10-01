using PowerlineTool.Core;
using Xunit;

namespace PowerlineTool.Tests;

static class Make
{
    public static PlcDevice Dev(string mac, params PlcLink[] links)
    {
        var d = new PlcDevice { Mac = mac };
        d.Links.AddRange(links);
        return d;
    }
}

public class ModelTests
{
    [Theory]
    [InlineData(0, LinkQuality.Poor)]
    [InlineData(49, LinkQuality.Poor)]
    [InlineData(50, LinkQuality.Fair)]
    [InlineData(99, LinkQuality.Fair)]
    [InlineData(100, LinkQuality.Good)]
    [InlineData(199, LinkQuality.Good)]
    [InlineData(200, LinkQuality.Excellent)]
    public void Quality_thresholds(int mbps, LinkQuality expected) => Assert.Equal(expected, Quality.Of(mbps));

    [Fact]
    public void DefaultName_uses_the_last_two_bytes_like_tpPLC() =>
        Assert.Equal("Device_f5e2", MacUtil.DefaultName("AA:BB:CC:00:F5:E2"));

    [Fact]
    public void Mac_roundtrips()
    {
        var bytes = MacUtil.Parse("AA:BB:CC:00:F5:E2");
        Assert.Equal("AA:BB:CC:00:F5:E2", MacUtil.Format(bytes));
    }

    [Fact]
    public void Summary_counts_each_link_once_and_finds_the_weakest()
    {
        var devices = DemoData.Devices(0);
        var s = Summary.Of(devices);
        Assert.Equal(3, s.Devices);
        Assert.Equal(2, s.Links);          // A-B and A-C, although each is reported from both ends
        Assert.Equal("AA:BB:CC:00:00:01", s.WeakestFrom);
        Assert.True(s.WeakestMbps < 100);
    }

    [Fact]
    public void Summary_without_links_is_empty_not_a_crash()
    {
        var s = Summary.Of(new[] { Make.Dev("A") });
        Assert.Equal(1, s.Devices);
        Assert.Equal(0, s.Links);
    }

    [Fact]
    public void Merge_combines_the_same_adapter_seen_on_two_cards()
    {
        var a = Make.Dev("A", new PlcLink("B", 100, 90)); a.Firmware = "";
        var b = Make.Dev("A", new PlcLink("B", 1, 1), new PlcLink("C", 50, 60)); b.Firmware = "fw"; b.IsLocal = true;
        var merged = Scanner.Merge(new[] { a, b });
        var d = Assert.Single(merged);
        Assert.True(d.IsLocal);
        Assert.Equal("fw", d.Firmware);
        Assert.Equal(2, d.Links.Count);                  // B kept from the first, C added
        Assert.Equal(100, d.Links.First(l => l.Peer == "B").Tx);
    }
}

public class ChangeDetectorTests
{
    static PlcDevice Pair(string a, string b, int tx, int rx) => Make.Dev(a, new PlcLink(b, tx, rx));

    [Fact]
    public void First_scan_reports_nothing()
    {
        Assert.Empty(ChangeDetector.Diff(null, new[] { Pair("A", "B", 10, 10) }, 50));
        Assert.Empty(ChangeDetector.Diff(new List<PlcDevice>(), new[] { Pair("A", "B", 10, 10) }, 50));
    }

    [Fact]
    public void Detects_a_device_that_disappeared_and_one_that_appeared()
    {
        var before = new[] { Make.Dev("A"), Make.Dev("B") };
        var after = new[] { Make.Dev("A"), Make.Dev("C") };
        var changes = ChangeDetector.Diff(before, after, 50);
        Assert.Contains(changes, c => c.Kind == ChangeKind.DeviceLost && c.Mac == "B");
        Assert.Contains(changes, c => c.Kind == ChangeKind.DeviceAppeared && c.Mac == "C");
    }

    [Fact]
    public void Reports_a_link_dropping_below_the_threshold_once_per_pair()
    {
        var before = new[] { Pair("A", "B", 200, 200), Pair("B", "A", 200, 200) };
        var after = new[] { Pair("A", "B", 30, 40), Pair("B", "A", 40, 30) };
        var low = ChangeDetector.Diff(before, after, 50).Where(c => c.Kind == ChangeKind.LinkLow).ToList();
        var one = Assert.Single(low);
        Assert.Equal(30, one.Mbps);
    }

    [Fact]
    public void Does_not_repeat_the_warning_while_the_link_stays_low()
    {
        var before = new[] { Pair("A", "B", 30, 30), Pair("B", "A", 30, 30) };
        var after = new[] { Pair("A", "B", 20, 20), Pair("B", "A", 20, 20) };
        Assert.Empty(ChangeDetector.Diff(before, after, 50));
    }

    [Fact]
    public void Reports_recovery()
    {
        var before = new[] { Pair("A", "B", 30, 30), Pair("B", "A", 30, 30) };
        var after = new[] { Pair("A", "B", 120, 120), Pair("B", "A", 120, 120) };
        Assert.Single(ChangeDetector.Diff(before, after, 50), c => c.Kind == ChangeKind.LinkRecovered);
    }

    [Fact]
    public void A_brand_new_low_link_is_reported()
    {
        var before = new[] { Make.Dev("A"), Make.Dev("B") };
        var after = new[] { Pair("A", "B", 10, 10), Pair("B", "A", 10, 10) };
        Assert.Single(ChangeDetector.Diff(before, after, 50), c => c.Kind == ChangeKind.LinkLow);
    }
}

public class HistoryTests
{
    [Fact]
    public void Series_is_oriented_from_the_requested_end()
    {
        var h = new HistoryStore();
        h.Add(DateTime.Now, new[] { Make.Dev("A", new PlcLink("B", 200, 100)), Make.Dev("B", new PlcLink("A", 100, 200)) });

        var ab = Assert.Single(h.Series("A", "B"));
        Assert.Equal((200, 100), (ab.Tx, ab.Rx));
        var ba = Assert.Single(h.Series("B", "A"));
        Assert.Equal((100, 200), (ba.Tx, ba.Rx));
    }

    [Fact]
    public void Uses_the_other_end_when_the_lower_mac_does_not_report()
    {
        var h = new HistoryStore();
        h.Add(DateTime.Now, new[] { Make.Dev("B", new PlcLink("A", 100, 200)) });
        var s = Assert.Single(h.Series("A", "B"));
        Assert.Equal((200, 100), (s.Tx, s.Rx));
    }

    [Fact]
    public void Keeps_only_the_newest_samples()
    {
        var h = new HistoryStore(capacity: 5);
        for (int i = 0; i < 12; i++) h.Add(DateTime.Now.AddSeconds(i), new[] { Make.Dev("A", new PlcLink("B", i, i)) });
        var s = h.Series("A", "B");
        Assert.Equal(5, s.Count);
        Assert.Equal(11, s[^1].Tx);
    }

    [Fact]
    public void Unknown_pair_is_empty_and_clear_works()
    {
        var h = new HistoryStore();
        Assert.Empty(h.Series("X", "Y"));
        h.Add(DateTime.Now, new[] { Make.Dev("A", new PlcLink("B", 1, 1)) });
        h.Clear();
        Assert.Empty(h.Pairs);
    }

    [Fact]
    public void Csv_has_one_row_per_link_and_a_stable_format()
    {
        var t = new DateTime(2026, 10, 1, 12, 0, 0);
        var lines = HistoryCsv.Lines(t, new[] { Make.Dev("A", new PlcLink("B", 200, 100), new PlcLink("C", 5, 6)) });
        Assert.Equal("2026-10-01T12:00:00,A,B,200,100\n2026-10-01T12:00:00,A,C,5,6\n", lines);
    }

    [Fact]
    public void Csv_file_gets_a_header_once()
    {
        var path = Path.Combine(Path.GetTempPath(), "plt-" + Guid.NewGuid() + ".csv");
        try
        {
            var d = new[] { Make.Dev("A", new PlcLink("B", 1, 2)) };
            HistoryCsv.Append(path, DateTime.Now, d);
            HistoryCsv.Append(path, DateTime.Now, d);
            var rows = File.ReadAllLines(path);
            Assert.Equal(HistoryCsv.Header, rows[0]);
            Assert.Equal(3, rows.Length);
        }
        finally { File.Delete(path); }
    }
}

public class SettingsTests
{
    static string Temp() => Path.Combine(Path.GetTempPath(), "plt-" + Guid.NewGuid(), "settings.json");

    [Fact]
    public void Roundtrip_keeps_values_and_names()
    {
        var path = Temp();
        try
        {
            var s = new Settings { Theme = "dark", WarnBelowMbps = 80, IntervalSec = 30, MinimizeToTray = true };
            s.Names["AA:BB"] = "Living room";
            s.Save(path);
            var back = Settings.Load(path);
            Assert.Equal("dark", back.Theme);
            Assert.Equal(80, back.WarnBelowMbps);
            Assert.True(back.MinimizeToTray);
            Assert.Equal("Living room", back.NameOf("AA:BB"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void Missing_or_corrupt_file_gives_defaults()
    {
        Assert.Equal(10, Settings.Load(Temp()).IntervalSec);

        var path = Temp();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");
        try { Assert.Equal("system", Settings.Load(path).Theme); }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        var path = Temp();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"IntervalSec\":0,\"WarnBelowMbps\":999999,\"Width\":1,\"Names\":null}");
        try
        {
            var s = Settings.Load(path);
            Assert.Equal(3, s.IntervalSec);
            Assert.Equal(1000, s.WarnBelowMbps);
            Assert.Equal(760, s.Width);
            Assert.NotNull(s.Names);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void Names_from_version_1_0_are_imported()
    {
        var path = Temp();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "names.json"), "{\"AA:BB\":\"Old name\"}");
        try { Assert.Equal("Old name", Settings.Load(path).NameOf("AA:BB")); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Unnamed_devices_get_the_default_name() => Assert.Equal("Device_0001", new Settings().NameOf("AA:BB:CC:00:00:01"));
}

public class DemoDataTests
{
    [Fact]
    public void Values_stay_positive_and_vary_over_time()
    {
        var seen = new HashSet<int>();
        for (int t = -50; t < 50; t++)
            foreach (var d in DemoData.Devices(t))
                foreach (var l in d.Links) { Assert.True(l.Tx > 0 && l.Rx > 0); seen.Add(l.Tx); }
        Assert.True(seen.Count > 20);
    }

    [Fact]
    public void Seed_fills_the_history()
    {
        var h = new HistoryStore();
        DemoData.Seed(h, points: 30);
        Assert.Equal(2, h.Pairs.Count());
        Assert.Equal(30, h.Series("AA:BB:CC:00:00:01", "AA:BB:CC:00:00:02").Count);
    }
}


