namespace PowerlineTool.Core;

/// <summary>Fake devices for <c>--demo</c>: screenshots, and trying the UI without hardware. Values drift over time.</summary>
public static class DemoData
{
    const string A = "AA:BB:CC:00:00:01", B = "AA:BB:CC:00:00:02", C = "AA:BB:CC:00:00:03";

    static int Wobble(int baseValue, int tick, double phase) =>
        Math.Max(5, baseValue + (int)Math.Round(Math.Sin(tick * 0.45 + phase) * baseValue * 0.10 + Math.Sin(tick * 1.7 + phase * 3) * baseValue * 0.04));

    public static List<PlcDevice> Devices(int tick)
    {
        int ab = Wobble(223, tick, 0), ba = Wobble(145, tick, 1), ac = Wobble(48, tick, 2), ca = Wobble(61, tick, 3);
        return new List<PlcDevice>
        {
            new() { Mac = A, IsLocal = true, Role = "CCo", Firmware = "tpver_demo_1.0", Nid = "00112233445566", Tei = 1, Nic = "Ethernet",
                    Links = { new PlcLink(B, ab, ba), new PlcLink(C, ac, ca) } },
            new() { Mac = B, Firmware = "tpver_demo_1.0", Nid = "00112233445566", Tei = 2, Nic = "Ethernet", Links = { new PlcLink(A, ba, ab) } },
            new() { Mac = C, Firmware = "tpver_demo_1.0", Nid = "00112233445566", Tei = 3, Nic = "Ethernet", Links = { new PlcLink(A, ca, ac) } },
        };
    }

    /// <summary>Fills a history store with the last <paramref name="points"/> samples, <paramref name="stepSec"/> apart.</summary>
    public static void Seed(HistoryStore history, int points = 90, int stepSec = 10)
    {
        var now = DateTime.Now;
        for (int i = points; i >= 1; i--)
            history.Add(now.AddSeconds(-i * stepSec), Devices(-i));
    }
}
