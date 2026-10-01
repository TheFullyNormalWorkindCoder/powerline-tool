# Security

## What the tool does on your computer and network

- Sends and receives **raw Ethernet frames** on wired adapters through Npcap, to talk to powerline adapters on the same LAN segment.
- Is **read-only**: it asks adapters for names, versions and link rates. It does not write settings, passwords or firmware.
- Stores `settings.json` (and optionally `history.csv`) in `%APPDATA%\PowerlineTool`. Nothing is sent anywhere.
- Makes one kind of internet request, **only when you click "Check for updates"**: a GET to the GitHub releases API.
- *Capture traffic (diagnostics)* writes every non-IP frame it sees to `powerline-sniff.txt` on your Desktop. Those files contain MAC addresses. The tool never uploads them; check them before sharing.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem. Use GitHub's private reporting:
*Security → Report a vulnerability* on the repository page.

Include what you found, how to reproduce it and which version. You will get an answer as soon as possible; this is a hobby project, so please be patient.

## Supported versions

Only the latest release receives fixes.
