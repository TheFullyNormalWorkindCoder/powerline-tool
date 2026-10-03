# Security

## What the tool does on your computer and network

- Sends and receives **raw Ethernet frames** on wired adapters through Npcap, to talk to powerline adapters on the same LAN segment.
- Is **read-only**: it asks adapters for names, versions and link rates. It does not write settings, passwords or firmware.
- Stores `settings.json` (and optionally `history.csv`) in `%APPDATA%\PowerlineTool`, plus the WebView2 profile in `%APPDATA%\PowerlineTool\WebView2`. Nothing is sent anywhere.
- Shows its interface in a WebView2 window from files compiled into the exe. The page is served with a Content-Security-Policy that forbids all network access (`connect-src 'none'`), navigation to any other address is cancelled, new windows are blocked, and the only addresses the app will open in your browser are GitHub (this project) and npcap.com.
- Makes one kind of internet request, **only when you click "Check for updates"**: a GET to the GitHub releases API.
- *Capture traffic (diagnostics)* writes every non-IP frame it sees to `powerline-sniff.txt` on your Desktop. Those files contain MAC addresses. The tool never uploads them; check them before sharing.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem. Use GitHub's private reporting:
*Security → Report a vulnerability* on the repository page.

Include what you found, how to reproduce it and which version. You will get an answer as soon as possible; this is a hobby project, so please be patient.

## Supported versions

Only the latest release receives fixes.
