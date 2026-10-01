# Changelog

All notable changes to this project. Format based on [Keep a Changelog](https://keepachangelog.com/), versions follow [SemVer](https://semver.org/).

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
