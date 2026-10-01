<div align="center">

<img src="docs/icon.png" width="96" alt="Powerline Tool icon">

# Powerline Tool

**A small, reliable Windows replacement for TP-Link's tpPLC utility.**
Find your powerline adapters and see the real link speed between them.

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0E7C8A)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![License](https://img.shields.io/badge/license-MIT-2EB872)

English · [Hrvatski](README.hr.md)

<img src="docs/screenshot.png" width="760" alt="Powerline Tool showing three adapters with TX/RX speed bars">

</div>

> The screenshot uses `--demo` mode (fake devices), so you can try the interface without any hardware.

## Why

tpPLC regularly fails to see adapters that are plugged in and working, gets stuck after a few scans, and needs a restart to recover. Powerline Tool does the same core job with a much simpler approach:

- **Scans every Ethernet card at once** (virtual, VPN and Wi-Fi adapters are skipped), so it never "picks the wrong card".
- **Retries every request** instead of giving up on the first lost frame.
- **Talks to adapters directly** (raw Ethernet frames through Npcap). No background helper service that can get out of sync with the window.
- **Read-only.** It only asks adapters for information. It never changes passwords, settings or firmware.

## Features

- Discovers local **and remote** adapters (the ones on the other end of your mains wiring)
- Live **TX / RX link speed** per connection, colour-coded (green ≥ 100 Mbps, amber ≥ 50, red below)
- Firmware identifier of each adapter
- **Auto-refresh** at an interval you choose
- **Rename devices** (stored locally, right-click a card)
- **Export to CSV**
- Built-in **traffic capture** for diagnostics, used to add support for new adapter models (see [CONTRIBUTING](CONTRIBUTING.md))
- English and Croatian UI (follows the Windows language)

## Supported hardware

| Chipset family | Protocol | Status |
|---|---|---|
| Broadcom BCM60xxx (e.g. **TP-Link TL-PA7017**) | EtherType `0x8912` | ✅ Tested |
| Qualcomm Atheros (AR7400 / QCA7420 / QCA7500 …) | HomePlug AV `0x88E1` | ⚠️ Implemented from public documentation, **not tested on real hardware** |

Other TP-Link models probably use one of these two families, but that is a guess. If you try it on another adapter, please [open an issue](../../issues/new/choose) and say what happened.

## Install

1. Install **[Npcap](https://npcap.com)** (needed to send and receive raw Ethernet frames). Defaults are fine.
2. Download `Powerline-Tool-win-x64.exe` from the [Releases](../../releases) page and run it. .NET is bundled, nothing else to install.
3. Close tpPLC while using this tool, because two programs querying the adapters can interfere with each other.

Connect the computer to a powerline adapter with a network cable. Being behind an unmanaged switch is fine.

## Usage

| Action | How |
|---|---|
| Scan now | **Scan** |
| Keep updating | tick **Auto-refresh every N s** |
| Rename / copy MAC | right-click a device card |
| Save results | **Export CSV** |
| Try without hardware | run with `--demo` |
| Force a language | run with `--lang=en` or `--lang=hr` |

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build -c Release
```

Single self-contained executable:

```powershell
dotnet publish src/PowerlineTool -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

## How it works

The adapters answer a handful of management messages sent as raw Ethernet frames. The Broadcom protocol was worked out by capturing what tpPLC sends on the author's own network and replaying the read-only requests. Everything that is known, and everything that is still a guess, is written down in **[docs/PROTOCOL.md](docs/PROTOCOL.md)**.

## Known limitations

- Windows only (Npcap + WinForms).
- Which adapter is "local" is decided with a heuristic (see the protocol notes) that matches tpPLC on the tested pair, but is not proven in general.
- Speeds are shown as the adapter reports them (`value & 0x3FFF`). They matched tpPLC (about 210 Mbps vs 212) on the tested pair, but the meaning of the high bits is a guess.
- No firmware update, password change, LED or power-saving control. Those commands have not been captured yet.

## Disclaimer

Not affiliated with, endorsed by, or supported by TP-Link. "TP-Link" and "tpPLC" are trademarks of their respective owners. This project contains no TP-Link code or assets. Protocol details were observed from network traffic on the author's own devices for the purpose of interoperability. Use at your own risk.

## License

[MIT](LICENSE)

