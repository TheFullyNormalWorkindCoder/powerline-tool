<div align="center">

<img src="docs/icon.png" width="96" alt="Ikona Powerline Toola">

# Powerline Tool

**Mala i pouzdana zamjena za TP-Linkov tpPLC na Windowsima.**
Pronađi powerline adaptere, prati pravu brzinu veze među njima i dobij obavijest kad veza padne.

[![build](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/actions/workflows/build.yml/badge.svg)](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/TheFullyNormalWorkindCoder/powerline-tool?color=0E7C8A)](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/releases/latest)
[![downloads](https://img.shields.io/github/downloads/TheFullyNormalWorkindCoder/powerline-tool/total?color=2EB872)](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/releases)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0E7C8A)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![License](https://img.shields.io/badge/license-MIT-2EB872)

[English](README.md) · Hrvatski

<img src="docs/screenshot.png" width="780" alt="Pregled uređaja: tri adaptera s trakama brzine u bojama">

</div>

> Sve snimke zaslona koriste `--demo` način rada (izmišljeni uređaji), pa cijelo sučelje možeš isprobati i bez adaptera.

<table>
<tr>
<td width="33%"><img src="docs/screenshot-map.png" alt="Karta mreže"><br><sub><b>Mapa</b>: adapteri i veze, debljina i boja prate brzinu</sub></td>
<td width="33%"><img src="docs/screenshot-history.png" alt="Graf povijesti brzine"><br><sub><b>Povijest</b>: TX/RX kroz vrijeme s crtom upozorenja</sub></td>
<td width="33%"><img src="docs/screenshot-dark.png" alt="Tamna tema"><br><sub><b>Tamna tema</b>: prema Windowsima ili po izboru</sub></td>
</tr>
</table>

## Zašto

tpPLC često ne vidi adaptere koji rade, zaglavi nakon nekoliko skeniranja i treba ga ponovno pokrenuti. Powerline Tool radi isti osnovni posao jednostavnije:

- **Skenira sve Ethernet kartice odjednom** (virtualne, VPN i Wi-Fi preskače), pa nikad ne "odabere krivu karticu".
- **Ponavlja svaki upit** umjesto da odustane nakon prvog izgubljenog paketa.
- **Razgovara s adapterima izravno** (sirovi Ethernet okviri preko Npcapa), bez pozadinskog procesa koji se može razići s prozorom.
- **Samo čita.** Adapterima postavlja samo upite, nikad ne mijenja lozinke, postavke ni firmware.
- **Bez telemetrije i računa.** Jedini mrežni zahtjev koji može poslati je "Provjeri ažuriranja", i to samo kad klikneš.

## Mogućnosti

**Vidi svoju mrežu**
- Lokalni **i udaljeni** adapteri (oni na drugom kraju kućne instalacije)
- **TX / RX** brzina po vezi uživo, s ocjenom kvalitete (Slabo / Prihvatljivo / Dobro / Odlično)
- **Mapa** mreže i jednoredni **sažetak**: broj uređaja, prosječna brzina, najslabija veza
- Firmware, ID mreže i kartica preko koje je adapter viđen (zadrži miš iznad kartice)

**Drži je na oku**
- **Graf povijesti** po vezi, s min / prosjek / max i crtom upozorenja
- **Auto-osvježavanje** u intervalu po izboru
- **Obavijesti** kad uređaj nestane ili se vrati, ili veza padne ispod tvog praga
- Radi u **traci** (system tray), po želji se **pokreće uz Windowse** (smanjeno)
- Neobavezni **zapis povijesti** u CSV za vlastite grafove

**Praktično**
- Preimenovanje uređaja (sprema se lokalno), kopiranje MAC-a, izvoz u **CSV** ili **JSON**, kopiranje tekstualnog sažetka
- Svijetla / tamna tema, engleski / hrvatski
- Jedna instanca (drugo pokretanje samo dovede prvu naprijed)
- Ugrađeno **snimanje prometa** koje omogućuje dodavanje novih modela ([CONTRIBUTING](CONTRIBUTING.md))

## Podržani hardver

| Obitelj čipova | Protokol | Status |
|---|---|---|
| Broadcom BCM60xxx (npr. **TP-Link TL-PA7017**) | EtherType `0x8912` | ✅ Testirano |
| Qualcomm Atheros (AR7400 / QCA7420 / QCA7500 …) | HomePlug AV `0x88E1` | ⚠️ Napisano prema javnoj dokumentaciji, **nije testirano na pravom uređaju** |

Ostali TP-Link modeli vjerojatno koriste jednu od te dvije obitelji, ali to je pretpostavka. Ako ga isprobaš na drugom adapteru, [otvori issue](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/issues/new/choose) i napiši što se dogodilo.

## Instalacija

1. Instaliraj **[Npcap](https://npcap.com)** (potreban za slanje i primanje sirovih Ethernet okvira). Zadane opcije su u redu.
2. Preuzmi `Powerline-Tool-win-x64.exe` s [najnovijeg izdanja](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/releases/latest) i pokreni. .NET je ugrađen, ništa drugo ne treba.
3. Ugasi tpPLC dok koristiš ovaj alat, jer dva programa koja istovremeno ispituju adaptere mogu jedan drugom smetati.

Računalo spoji mrežnim kabelom na powerline adapter. Spoj preko običnog (unmanaged) switcha je u redu.

**Provjera preuzimanja** (neobavezno): svako izdanje navodi SHA-256 sažetak.

```powershell
(Get-FileHash .\Powerline-Tool-win-x64.exe -Algorithm SHA256).Hash
```

Windows SmartScreen može upozoriti na nepotpisanu aplikaciju nepoznatog izdavača; ako se sažetak poklapa, odaberi *More info → Run anyway*.

## Korištenje

| Radnja | Kako |
|---|---|
| Skeniraj odmah | **Skeniraj** ili `F5` |
| Promjena pogleda | **Uređaji / Mapa / Povijest** ili `Ctrl+1/2/3` |
| Preimenuj, kopiraj MAC, prikaži povijest | desni klik na karticu uređaja |
| Postavke (tema, jezik, pragovi, traka, pokretanje) | izbornik `⋯` → Postavke, ili `Ctrl+,` |
| Izvoz | izbornik `⋯` → CSV (`Ctrl+E`) / JSON / kopiraj sažetak |
| Prikaži ili sakrij log | `Ctrl+L` |

Prekidači naredbenog retka: `--demo` (izmišljeni uređaji), `--lang=en` ili `--lang=hr`, `--theme=light` ili `--theme=dark`, `--page=devices|map|history`, `--minimized`.

Datoteke su u `%APPDATA%\PowerlineTool`: `settings.json` (postavke i imena uređaja) i, ako je uključeno, `history.csv`.

## Rješavanje problema

**"Nije pronađen nijedan powerline adapter"**
1. Je li instaliran **Npcap**? (Aplikacija javi ako nedostaje.)
2. Je li **tpPLC ugašen**? Zatvori ga i iz trake.
3. Je li računalo spojeno na adapter **kabelom** (ne Wi-Fi-jem) i svijetli li lampica veze?
4. Otvori log (`Ctrl+L`) i klikni **Skeniraj**. Ako okviri stižu, a ništa se ne prepoznaje, tvoj model govori varijantu protokola koju ovaj alat još ne poznaje. Koristi *Snimaj promet* i priloži datoteku uz [prijavu adaptera](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/issues/new/choose).

**Brzine se razlikuju od tpPLC-a.** Oba čitaju istu vrijednost iz adaptera, a powerline brzine se iz sekunde u sekundu mijenjaju zbog električnih smetnji.

## Gradnja iz izvornog koda

Treba [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build -c Release
dotnet test  -c Release                       # parsiranje protokola, promjene, povijest, postavke
dotnet run --project src/PowerlineTool -- --demo
```

Jedna samostalna izvršna datoteka:

```powershell
dotnet publish src/PowerlineTool -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

Ponovno generiranje snimki zaslona: `powershell -File tools/make-screenshots.ps1`.

## Kako radi

Adapteri odgovaraju na nekoliko upravljačkih poruka poslanih kao sirovi Ethernet okviri. Broadcomov protokol je otkriven snimanjem onoga što tpPLC šalje na autorovoj vlastitoj mreži i ponavljanjem upita koji samo čitaju. Što se zna, a što je još pretpostavka, piše u **[docs/PROTOCOL.md](docs/PROTOCOL.md)**, a raspored koda u **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** (oboje na engleskom).

## Poznata ograničenja

- Samo Windows (Npcap + WinForms).
- Koji je adapter "lokalni" određuje se heuristikom koja se na testiranom paru poklapa s tpPLC-om, ali nije dokazana općenito.
- Brzine su prikazane kako ih adapter javlja (`vrijednost & 0x3FFF`). Na testiranom paru poklapaju se s tpPLC-om (oko 210 prema 212 Mbps), ali značenje gornjih bitova je pretpostavka.
- Nema ažuriranja firmwarea, promjene lozinke, LED-a, restarta ni štednje energije. Te naredbe nisu snimljene, a alat je namjerno samo za čitanje.
- Qualcomm putanja koda nikad nije pokrenuta na pravom adapteru.

## Doprinos

Testiranje na drugim modelima adaptera je najvrjednija pomoć. Vidi [CONTRIBUTING.md](CONTRIBUTING.md). Promjene su u [CHANGELOG-u](CHANGELOG.md), a sigurnosne prijave idu preko [SECURITY.md](SECURITY.md).

## Odricanje

Nije povezano s TP-Linkom, niti ga TP-Link podržava ili preporučuje. "TP-Link" i "tpPLC" su zaštitni znakovi njihovih vlasnika. Projekt ne sadrži TP-Linkov kod ni materijale. Detalji protokola uočeni su iz mrežnog prometa na autorovim vlastitim uređajima radi interoperabilnosti. Koristiš na vlastitu odgovornost.

## Licenca

[MIT](LICENSE)
