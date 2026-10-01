# Contributing

Thanks for helping! The most valuable contribution right now is **testing on adapters other than the TL-PA7017**.

## Report how it works on your adapter

Open an issue using the *Adapter report* template and include:

- adapter model and hardware version (printed on the label)
- what the app showed (a screenshot is great), and what the vendor's own utility showed
- the **log** (bottom of the window, *Show log*)

## Add support for a new model

1. Close the vendor utility, start Powerline Tool, click **Capture traffic (diagnostics)**.
2. Open the vendor utility, let it scan, then stop the capture.
3. The capture is saved as `powerline-sniff.txt` on the Desktop. **It contains MAC addresses**; replace them with dummy ones if you want to keep them private.
4. Attach it to an issue, or open a pull request that decodes it in `src/PowerlineTool/Plc.cs` and documents it in `docs/PROTOCOL.md`.

## Development

```powershell
dotnet build -c Release
dotnet run --project src/PowerlineTool -- --demo      # UI without hardware
```

Guidelines:

- Keep the tool **read-only**. Anything that writes to an adapter (passwords, firmware, reset) needs a clear confirmation in the UI and should be discussed in an issue first.
- Mark anything in `docs/PROTOCOL.md` that is a guess as a guess.
- User-visible strings go through `L.T("English", "Hrvatski")` in `Program.cs`.
- Do not commit captures that contain real MAC addresses.
