using PowerlineTool.Core;
using Xunit;

namespace PowerlineTool.Tests;

public class DeviceMemoryTests
{
    static PlcDevice Dev(string mac, bool local = false, params PlcLink[] links)
    {
        var d = new PlcDevice { Mac = mac, IsLocal = local, Firmware = "fw" };
        d.Links.AddRange(links);
        return d;
    }

    static readonly DateTime T0 = new(2026, 10, 3, 12, 0, 0);

    [Fact]
    public void An_adapter_that_goes_missing_stays_listed_but_stale_until_the_limit()
    {
        var m = new DeviceMemory(maxMissed: 3);
        m.Update(new[] { Dev("A", true), Dev("B") }, T0);

        var r1 = m.Update(new[] { Dev("A", true) }, T0.AddSeconds(5));
        var b = Assert.Single(r1, d => d.Mac == "B");
        Assert.True(b.Stale);
        Assert.Equal(1, b.Missed);

        m.Update(new[] { Dev("A", true) }, T0.AddSeconds(10));
        var r3 = m.Update(new[] { Dev("A", true) }, T0.AddSeconds(15));
        Assert.DoesNotContain(r3, d => d.Mac == "B");   // third miss in a row: gone
    }

    [Fact]
    public void Coming_back_resets_the_counter()
    {
        var m = new DeviceMemory(3);
        m.Update(new[] { Dev("A") }, T0);
        m.Update(Array.Empty<PlcDevice>(), T0.AddSeconds(5));
        var back = Assert.Single(m.Update(new[] { Dev("A") }, T0.AddSeconds(10)));
        Assert.False(back.Stale);
        Assert.Equal(T0.AddSeconds(10), back.LastSeen);
    }

    [Fact]
    public void Lost_link_data_is_carried_over_and_flagged()
    {
        var m = new DeviceMemory();
        m.Update(new[] { Dev("A", true, new PlcLink("B", 200, 100)) }, T0);

        var now = Assert.Single(m.Update(new[] { Dev("A", true) }, T0.AddSeconds(5)));   // answered, but no link data
        Assert.False(now.Stale);
        Assert.True(now.LinksStale);
        Assert.Equal(200, Assert.Single(now.Links).Tx);
    }

    [Fact]
    public void Fresh_link_data_replaces_the_old_and_clears_the_flag()
    {
        var m = new DeviceMemory();
        m.Update(new[] { Dev("A", true, new PlcLink("B", 200, 100)) }, T0);
        var now = Assert.Single(m.Update(new[] { Dev("A", true, new PlcLink("B", 50, 40)) }, T0.AddSeconds(5)));
        Assert.False(now.LinksStale);
        Assert.Equal(50, Assert.Single(now.Links).Tx);
    }

    [Fact]
    public void Local_adapter_is_listed_first()
    {
        var m = new DeviceMemory();
        var list = m.Update(new[] { Dev("A"), Dev("Z", true), Dev("B") }, T0);
        Assert.Equal(new[] { "Z", "A", "B" }, list.Select(d => d.Mac));
    }
}

public class ScanCoordinatorTests
{
    static ScanResult R(params PlcDevice[] devices) => new() { Devices = devices.ToList(), Nics = new List<string> { "Ethernet" } };
    static PlcDevice Dev(string mac, params PlcLink[] links)
    {
        var d = new PlcDevice { Mac = mac };
        d.Links.AddRange(links);
        return d;
    }

    static ScanCoordinator Make(Queue<ScanResult> script, out List<int> calls)
    {
        var count = new List<int>();
        calls = count;
        var c = new ScanCoordinator(new Settings(), () => { count.Add(1); return script.Count > 1 ? script.Dequeue() : script.Peek(); }) { RetryDelayMs = 1 };
        return c;
    }

    [Fact]
    public async Task An_empty_scan_is_retried_once_before_it_is_believed()
    {
        var script = new Queue<ScanResult>(new[] { R(Dev("A")), R(), R(Dev("A")) });
        var c = Make(script, out var calls);

        await c.ScanAsync();                 // finds A
        var outcome = await c.ScanAsync();   // first try empty, retry finds A again
        Assert.Equal(3, calls.Count);
        Assert.Single(c.Devices);
        Assert.False(c.Devices[0].Stale);
        Assert.Empty(outcome.Changes);
    }

    [Fact]
    public async Task A_persistent_miss_marks_the_adapter_stale_and_only_later_reports_it_lost()
    {
        var script = new Queue<ScanResult>(new[] { R(Dev("A"), Dev("B")), R(Dev("A")) });
        var c = Make(script, out _);

        await c.ScanAsync();
        var o2 = await c.ScanAsync();
        Assert.True(c.Devices.First(d => d.Mac == "B").Stale);
        Assert.DoesNotContain(o2.Changes, ch => ch.Kind == ChangeKind.DeviceLost);

        await c.ScanAsync();
        var o4 = await c.ScanAsync();
        Assert.DoesNotContain(c.Devices, d => d.Mac == "B");
        Assert.Contains(c.Events, e => e.Kind == ChangeKind.DeviceLost && e.Mac == "B");
        Assert.NotNull(o4);
    }

    [Fact]
    public async Task Stale_data_is_not_written_into_the_history()
    {
        var script = new Queue<ScanResult>(new[] { R(Dev("A", new PlcLink("B", 100, 90)), Dev("B", new PlcLink("A", 90, 100))), R() });
        var c = Make(script, out _);
        await c.ScanAsync();
        await c.ScanAsync();     // everything missing -> stale
        Assert.Single(c.History.Series("A", "B"));
    }

    [Fact]
    public async Task Two_scans_at_once_do_not_overlap()
    {
        var gate = new TaskCompletionSource();
        var c = new ScanCoordinator(new Settings(), () => { gate.Task.Wait(); return R(Dev("A")); });
        var first = c.ScanAsync();
        await Task.Delay(30);
        Assert.True(c.Scanning);
        Assert.Null(await c.ScanAsync());     // second call is refused, not queued
        gate.SetResult();
        Assert.NotNull(await first);
        Assert.False(c.Scanning);
    }

    [Fact]
    public async Task Scanner_exceptions_release_the_lock()
    {
        int n = 0;
        var c = new ScanCoordinator(new Settings(), () => { if (n++ == 0) throw new DllNotFoundException("npcap"); return R(Dev("A")); });
        await Assert.ThrowsAsync<DllNotFoundException>(() => c.ScanAsync());
        Assert.False(c.Scanning);
        Assert.NotNull(await c.ScanAsync());
    }
}
