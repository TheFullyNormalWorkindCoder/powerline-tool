# Architecture

```
src/
├── PowerlineTool.Core/       net8.0, no UI. Everything that talks to adapters or decides things.
│   ├── Models.cs             PlcDevice, PlcLink, quality ratings, MAC helpers, network Summary
│   ├── NicSession.cs         raw frames on one adapter through Npcap (send, receive, retry)
│   ├── BcmProtocol.cs        Broadcom 0x8912: frame building, parsing, scan
│   ├── QcaProtocol.cs        Qualcomm 0x88E1: parsing, scan (untested on hardware)
│   ├── Scanner.cs            which cards to use (and why others are skipped), parallel scan, merge; traffic Sniffer
│   ├── DeviceMemory.cs       remembers adapters between scans (stale after a miss, dropped after three)
│   ├── ScanCoordinator.cs    one scan + the books: retry, memory, history, change events
│   ├── ChangeDetector.cs     what changed between two scans (lost, back, link low, recovered)
│   ├── History.cs            rate history per link, CSV log
│   ├── Settings.cs           settings.json and device names
│   ├── UiState.cs            the JSON the web interface renders
│   ├── Exporter.cs           CSV / JSON export
│   └── DemoData.cs           fake devices for --demo
└── PowerlineTool/            net8.0-windows
    ├── Program.cs            single instance, settings, theme; picks the web or the classic window
    ├── Web/WebMainForm.cs    WebView2 host: serves ui/ from the exe, relays commands, tray, files, timers
    ├── ui/                   the interface: index.html, app.css, app.js, charts.js, i18n.js (embedded in the exe)
    ├── Forms/, Controls/     the classic WinForms window (fallback)
    ├── Theme.cs, Support.cs  palettes and localisation helper (classic), options, startup, update check
tests/PowerlineTool.Tests/    xUnit; pure logic only, so no adapter or Npcap is needed
tools/make-screenshots.ps1    regenerates the README screenshots from --demo mode
```

## Data flow of one scan

1. `ScanCoordinator.ScanAsync` runs the scanner function (`Scanner.ScanAllDetailed`, or `DemoData` with `--demo`).
2. `Scanner` lists suitable wired cards (`ListNics`, with a reason for every card it skips) and scans each in parallel with its own `NicSession`. `BcmProtocol` goes first (broadcast hello, then per adapter: network ID and link rates); `QcaProtocol` only if nothing answered. Results from all cards are merged by MAC.
3. An empty result after adapters were seen is retried once. Then `DeviceMemory` merges the scan into what is remembered: missing adapters become stale, lost link data is carried over.
4. History is appended (fresh data only), `ChangeDetector` produces events, and the window pushes the new state to the page.

## How the web interface talks to C#

```
 C# (WebMainForm)                                         page (ui/app.js)
 ───────────────                                          ───────────────
 UiState.Build(...)  ── PostWebMessageAsJson ─────────▶   {type:"state", state:{devices, history, settings, ...}}
 events / toasts     ── {type:"toast" | "log" | ...} ──▶   renders pages, toasts, activity drawer
 ScanNow, Save, ...  ◀── chrome.webview.postMessage ───   {cmd:"scan" | "settings" | "rename" | "export" | ...}
```

- The page is served from resources compiled into the exe at `https://app.powerline/`, with a strict Content-Security-Policy (`connect-src 'none'`): it cannot make network requests of its own.
- Navigation away from that address is cancelled, new windows are blocked, and the `open` command only accepts GitHub and npcap.com addresses.
- Opened in a normal browser (no WebView2), `app.js` falls back to a built-in demo, which is how the interface is developed and previewed without hardware.
- WebView2 may only be touched on the UI thread; everything posted from scanner threads goes through `Post`, which marshals.

## Design rules

- **Core has no UI and no Windows-only API**, so the logic is testable on any machine. Parsing functions take bytes and return values; only `NicSession` and `Scanner` touch Npcap.
- **Read-only.** There is no code path that writes to an adapter.
- **Fail soft.** Truncated or unexpected frames return "nothing" instead of throwing; settings fall back to defaults; if WebView2 cannot start, the classic window opens.
- **Say what is wrong.** An empty screen always comes with a reason (no wired card, Npcap missing, nobody answered, another program running).
- **Everything user-visible is localised**: `ui/i18n.js` for the web interface, `L.T("English", "Hrvatski")` in C#.
- **Protocol facts live in one place**: `docs/PROTOCOL.md`, with guesses marked as guesses.
