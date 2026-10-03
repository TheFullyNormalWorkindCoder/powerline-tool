# Contributing

Thanks for helping! The most valuable contribution right now is **testing on adapters other than the TL-PA7017**.

## Report how it works on your adapter

Open an issue using the *Adapter report* template and include:

- adapter model and hardware version (printed on the label)
- what the app showed (a screenshot is great), and what the vendor's own utility showed
- the **log** (`Ctrl+L`, then press Scan)

## Add support for a new model

1. Close the vendor utility, start Powerline Tool, click **Capture traffic (diagnostics)** at the bottom right.
2. Open the vendor utility, let it scan, then stop the capture.
3. The capture is saved as `powerline-sniff.txt` on the Desktop. **It contains MAC addresses**; replace them with dummy ones if you want to keep them private.
4. Attach it to an issue, or open a pull request that decodes it in `src/PowerlineTool.Core/` and documents it in `docs/PROTOCOL.md`.

## Development

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build -c Release
dotnet test  -c Release
dotnet run --project src/PowerlineTool -- --demo      # UI without hardware
```

The code layout is described in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Guidelines

- Keep the tool **read-only**. Anything that writes to an adapter (passwords, firmware, reset) needs a clear confirmation in the UI and should be discussed in an issue first.
- Protocol code goes in `PowerlineTool.Core` as small pure functions with a unit test (use a real captured frame with the MAC addresses replaced).
- Mark anything in `docs/PROTOCOL.md` that is a guess as a guess.
- User-visible strings go in `src/PowerlineTool/ui/i18n.js` (both languages). The classic window uses `L.T("English", "Hrvatski")`.
- The interface (`src/PowerlineTool/ui`) must stay dependency-free: no CDN, fonts or images from the internet. The page is served with `connect-src 'none'`, so it cannot fetch anything anyway.
- Work on the interface without Windows or hardware: serve the `ui` folder with any static server and open it in a browser; it runs on demo data (`?down=1` simulates an unreachable partner, `?empty=1` an empty network).
- Do not commit captures that contain real MAC addresses.
- If you change the UI, regenerate the screenshots: `powershell -File tools/make-screenshots.ps1`.
