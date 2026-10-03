# Changelog

All notable changes to this project. Format based on [Keep a Changelog](https://keepachangelog.com/), versions follow [SemVer](https://semver.org/).

## [1.2.0] - 2026-10-03

### Added
- **New interface** (HTML/CSS/JS shown in a WebView2 window): sidebar navigation, health gauge with summary, device cards with animated TX/RX bars and sparklines, animated **network map**, interactive **history chart** (hover tooltip, range buttons, min/avg/max), light and dark themes, six accent colours, English and Croatian, responsive layout, reduced-motion option.
- **Memory between scans:** an adapter that stops answering stays listed as "not seen" for three scans instead of vanishing after one lost reply. Last known link speeds are kept when a reply with the speeds is lost.
- **Automatic retry:** an empty scan after adapters were seen is repeated once before it is believed.
- **Clear states for what is actually wrong:** "only the local adapter answers", "partner not answering" (listed by the local adapter but 0/0), no wired card, Npcap missing, tpPLC running at the same time. A 0/0 link is shown as "no link", not as "0 Mbps, poor", and no longer drags the average down.
- Command palette (`Ctrl+K` or `/`), pause/resume auto-refresh, quick theme switch, always-on-top, sort cards, expand/collapse all, copy summary, save history of a link as CSV, **diagnostics for a bug report** (copy or save, MAC addresses shortened), test notification, reset settings.
- `--classic` starts the previous Windows-style window; it is also used automatically if WebView2 is not available. New switches: `--accent`, `--shot`, `--devtools`.
- Tests for device memory, the scan coordinator, the UI state JSON and the new summary rules (70 in total).

### Changed
- Scanning logic moved out of the window into `ScanCoordinator` (Core), shared by both interfaces.
- Each scan gets fresh capture devices, and event handlers are removed when a scan ends. Before, they piled up on the cached device objects.
- The broadcast discovery ends 400 ms after the last reply instead of waiting the full time, so a scan takes about 0.6 s instead of 1.5 s.
- The page cannot reach the network at all (Content-Security-Policy) and may only open GitHub and npcap.com links.

### Fixed
- A scan that found only some adapters on the first try used to blank the whole list on the next empty reply.
- Log lines written from scanner threads could crash the web window ("WebView2 can only be accessed from the UI thread").

## [1.1.0] - 2026-10-01

### Added
- **Map** view: adapters as nodes, links coloured and sized by speed.
- **History** view: TX/RX chart per link with min / avg / max and a warning line.
- Link **quality rating** (Poor / Fair / Good / Excellent) and a network **summary** (devices, links, average, weakest link).
- **Notifications** when a device disappears or returns, or a link drops below a configurable threshold.
- **System tray** mode, optional **start with Windows** (minimised).
- **Dark theme** (follows Windows or set manually) and **settings** dialog.
- Optional **history log** to CSV; **JSON export**; copy summary to clipboard.
- Manual **Check for updates**, About dialog, keyboard shortcuts (`F5`, `Ctrl+1/2/3`, `Ctrl+E`, `Ctrl+,`, `Ctrl+L`, `F1`).
- Device details tooltip (firmware, network ID, TEI, role, adapter).
- Single-instance behaviour: launching twice brings the running window forward.
- Unit tests (52) for protocol parsing, change detection, history, settings; CI runs them.
- Command-line switches `--page`, `--theme`, `--minimized` next to `--demo` and `--lang`.

### Changed
- Code split into `PowerlineTool.Core` (protocol, no UI) and the WinForms app.
- Settings and device names now live in one `settings.json` (names from 1.0 are imported automatically).
- Bluetooth network adapters are skipped when scanning.

### Removed
- The (non-working for Broadcom) restart code, so the "read-only" promise is true in the code as well.

## [1.0.0] - 2026-10-01

### Added
- First release: scan local and remote adapters, TX/RX speed per link, Broadcom (TL-PA7017) support, experimental Qualcomm support, auto-refresh, rename, CSV export, traffic capture, English and Croatian UI.
