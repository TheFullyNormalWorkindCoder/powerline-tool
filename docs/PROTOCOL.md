# Protocol notes

What Powerline Tool sends to powerline adapters, and what it understands from the answers.

> **Status: observed, not official.** The Broadcom part below was reverse-engineered by capturing the traffic of TP-Link's tpPLC utility between a PC and a pair of TL-PA7017 adapters, then replaying the read-only requests. No vendor documentation was used. Anything marked *guess* is an interpretation that fits the captures, not something confirmed.

All frames are plain Layer-2 Ethernet frames, so the PC must be on the same LAN segment as an adapter (an unmanaged switch in between is fine).

## 1. Broadcom adapters (EtherType `0x8912`)

Tested on: TP-Link TL-PA7017 (firmware id `tpver_701E14_190426_901`, reports `"bcm": 1` in tpPLC's log).

### Frame layout

```
 0        6        12      14   15   16                 22   23 …
 +--------+--------+-------+----+----+------------------+----+---------
 | dst    | src    | 89 12 | ver| op | A0 00 00 00 1F 84| id | args…
 +--------+--------+-------+----+----+------------------+----+---------
```

| Field | Notes |
|---|---|
| `ver` | `0x01` on the discovery request, `0x02` on everything else (requests and replies) |
| `op` | request opcode; the reply uses `op + 1` |
| `A0 00 00 00 1F 84` | constant in every captured frame (*guess:* protocol/session marker) |
| `id` | random one-byte request id, **echoed by the reply**. Used to match replies to requests |
| args | opcode specific |

Frames are padded with zeros to the 60-byte Ethernet minimum.

### Opcodes used

#### `0x70` / `0x71` Hello (discovery)

Sent to the **broadcast** address. Every adapter on the powerline network answers, local and remote, each from its own MAC.

Request body (45 bytes, identical in every capture, treated as opaque):

```
01 70 A0 00 00 00 1F 84 01 A3 97 A2 55 53 BE F1 FC F9 79 6B 52 14 13 E9 E2
00 0F 94 CA 6F 98 0D 61 01 00 00 60 01 88 03 FA 00 FC FA 22
```

Reply body: `02 71 <hdr> 01 <index> <len> <ASCII firmware id>`

- `index`: `1` for the adapter plugged into the PC, `2` for the next one, and so on in the captures. Used as the **"local" heuristic** (*guess*; it matched tpPLC on the tested pair).
- `len`: length of the firmware id string (`0x17` = 23 for `tpver_701E14_190426_901`).

#### `0x5C` / `0x5D` Get variable

Unicast to one adapter. Request body: `02 5C <hdr> <id> <var>`.
Reply body: `02 5D <hdr> <id> 01 <len:u16 LE> <data>`.

| `var` | Data | Used for |
|---|---|---|
| `0x23` | 7 bytes | **Network ID (NID)**. Needed for the rates request |
| `0x1B` | 64 bytes, ASCII, zero padded | Firmware id string |
| `0x24` | 16 bytes | Not interpreted (*guess:* key related, deliberately not displayed or exported) |
| others | | Not decoded. tpPLC also reads `0x09 0x0A 0x0B 0x0C 0x3E 0x3F 0x69 0x95` |

#### `0x2C` / `0x2D` Station link rates

Unicast to one adapter. Request body: `02 2C <hdr> <id> 01 <NID:7>`.
Reply body: `02 2D <hdr> <id> <count> { <peer MAC:6> <tx:u16 LE> <rx:u16 LE> } × count`

The rate is the low 14 bits of each `u16` (`value & 0x3FFF`). In the captures the raw value was `0x40D4` while tpPLC showed about 210 Mbps, and `0xD4` = 212. The `0x40` in the high byte is treated as a flag (*guess*).

The two directions are mirrored between the two ends: adapter A reports `(tx=212, rx=146)` for B, and B reports `(tx=146, rx=212)` for A.

### Observed but not implemented

| Frame | Seen | Notes |
|---|---|---|
| op `0x4C` / `0x4D` | 317-byte reply | Looks like a block of counters / statistics |
| op `0x5C` var `0x69` | 1026-byte reply | Large table, not decoded |
| op `0x60` / `0x61` | short reply | Not decoded |
| EtherType `0x22E3` broadcast | sent once per scan by tpPLC | Not decoded; not needed for scanning |

## 2. Qualcomm Atheros adapters (EtherType `0x88E1`, HomePlug AV)

Implemented from the public [open-plc-utils](https://github.com/qca/open-plc-utils) documentation. **Not tested on real hardware.**

- Vendor frames use OUI `00:B0:52`.
- `VS_SW_VER` (`0xA000` → `0xA001`): version string.
- `VS_NW_INFO` (`0xA038` → `0xA039`): networks and stations with average TX/RX. The station record size differs between chipset generations, so the parser derives it from the payload length and logs a hex dump.

Broadcom adapters answer these requests with an error frame (type `0x6046`, echoing the request type), which is how it was found that the TL-PA7017 is not a Qualcomm device. The scanner therefore tries Broadcom first and falls back to Qualcomm only if nothing answers.

## 3. Capturing a new adapter model

1. Start the capture from the app: **Capture traffic (diagnostics)**. It writes `powerline-sniff.txt` to the Desktop and records every non-IP frame on all Ethernet adapters.
2. Open the vendor's own utility, let it scan, then stop the capture.
3. Replace the MAC addresses in the file if you do not want to share them, and attach it to an issue.

Frames to look at: the first frames sent to the broadcast address, and the request/reply pairs that follow. See [CONTRIBUTING](../CONTRIBUTING.md).
