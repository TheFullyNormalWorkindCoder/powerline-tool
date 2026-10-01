# Architecture

```
src/
├── PowerlineTool.Core/     net8.0, no UI. Everything that talks to adapters or decides things.
│   ├── Models.cs           PlcDevice, PlcLink, quality ratings, MAC helpers, network Summary
│   ├── NicSession.cs       raw frames on one adapter through Npcap (send, receive, retry)
│   ├── BcmProtocol.cs      Broadcom 0x8912: frame building, parsing, scan
│   ├── QcaProtocol.cs      Qualcomm 0x88E1: parsing, scan (untested on hardware)
│   ├── Scanner.cs          which adapters to use, scan them in parallel, merge; traffic Sniffer
│   ├── ChangeDetector.cs   what changed between two scans (lost, back, link low, recovered)
│   ├── History.cs          rate history per link, CSV log
│   ├── Settings.cs         settings.json and device names
│   └── DemoData.cs         fake devices for --demo
└── PowerlineTool/          net8.0-windows, WinForms
    ├── Program.cs          single instance, settings, theme, start the window
    ├── Theme.cs            light/dark palettes, localisation helper (L.T)
    ├── Support.cs          command-line options, start-with-Windows, update check
    ├── Controls/           DeviceCard, MapView, ChartView, NavButton (all custom painted)
    └── Forms/              MainForm, SettingsForm, AboutForm, dialogs
tests/PowerlineTool.Tests/  xUnit; pure logic only, so no adapter or Npcap is needed
```

## Data flow of one scan

1. `Scanner.ScanAll` lists suitable wired adapters (`ListNics`) and scans each in parallel with its own `NicSession`.
2. `Scanner.Scan` asks `BcmProtocol` first (broadcast hello, then per adapter: network ID and link rates). If nothing answers it falls back to `QcaProtocol`.
3. Results from all adapters are merged by MAC address.
4. The UI diffs the result against the previous scan (`ChangeDetector`), appends it to `HistoryStore`, re-renders the cards and map, and raises tray notifications for changes.

## Design rules

- **Core has no UI and no Windows-only API**, so the logic is testable on any machine. Parsing functions take bytes and return values; only `NicSession` and `Scanner` touch Npcap.
- **Read-only.** There is no code path that writes to an adapter.
- **Fail soft.** Truncated or unexpected frames return "nothing" instead of throwing; settings fall back to defaults.
- **Everything user-visible is localised** through `L.T("English", "Hrvatski")`.
- **Protocol facts live in one place**: `docs/PROTOCOL.md`, with guesses marked as guesses.
