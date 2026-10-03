<div align="center">

<img src="docs/icon.png" width="96" alt="Ikona Powerline Toola">

# Powerline Tool

**Mala i pouzdana zamjena za TP-Linkov tpPLC na Windowsima.**
Pronađi powerline adaptere, prati pravu brzinu svake veze i dobij obavijest kad neka padne.

[![build](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/actions/workflows/build.yml/badge.svg)](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/TheFullyNormalWorkindCoder/powerline-tool?color=0E7C8A)](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/releases/latest)
[![downloads](https://img.shields.io/github/downloads/TheFullyNormalWorkindCoder/powerline-tool/total?color=2EB872)](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/releases)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0E7C8A)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![License](https://img.shields.io/badge/license-MIT-2EB872)

[English](README.md) · Hrvatski

<img src="docs/screenshot.png" width="800" alt="Pregled uređaja: mjerač zdravlja, sažetak i kartice uređaja s trakama TX/RX u bojama">

</div>

> **Neslužbeni projekt.** Ovo je neovisan, zajednički alat. **Nije izrađen, odobren, podržan niti na bilo koji način povezan s TP-Linkom.** "TP-Link" i "tpPLC" su zaštitni znakovi njihovih vlasnika i ovdje se koriste samo da se kaže s kojim je hardverom alat kompatibilan i koji program zamjenjuje.

> Sve snimke zaslona koriste `--demo` način rada (izmišljeni uređaji), pa cijelo sučelje možeš isprobati i bez adaptera. Snimke su na engleskom; aplikacija se prebacuje na hrvatski prema Windowsima ili u postavkama.

<table>
<tr>
<td width="50%"><img src="docs/screenshot-map.png" alt="Karta mreže s animiranim vezama"><br><sub><b>Mapa</b>: adapteri i veze, debljina i boja linije prate brzinu, a tok se brže animira na bržim vezama</sub></td>
<td width="50%"><img src="docs/screenshot-history.png" alt="Graf povijesti brzine"><br><sub><b>Povijest</b>: TX/RX po vezi s vrijednostima pri prelasku mišem, rasponi, min / prosjek / max</sub></td>
</tr>
<tr>
<td><img src="docs/screenshot-light.png" alt="Svijetla tema s plavim naglaskom"><br><sub><b>Svijetla tema</b> s drugom bojom naglaska. Prema Windowsima ili ručno</sub></td>
<td><img src="docs/screenshot-settings.png" alt="Stranica postavki"><br><sub><b>Postavke</b>: tema, naglasak, jezik, pragovi, traka, pokretanje, alati</sub></td>
</tr>
</table>

## Zašto

tpPLC često ne vidi adaptere koji rade, zaglavi nakon nekoliko skeniranja i treba ga ponovno pokrenuti. Powerline Tool radi isti osnovni posao jednostavnije:

- **Skenira sve Ethernet kartice odjednom** (virtualne, VPN i Wi-Fi preskače, i kaže koje i zašto), pa nikad ne "odabere krivu karticu".
- **Ponavlja i pamti.** Izgubljen odgovor ne uzrokuje nestanak adaptera: ostaje na zaslonu kao "nije viđen" nekoliko skeniranja, a prazno skeniranje se jednom ponovi prije nego mu povjeruje.
- **Razgovara s adapterima izravno** (sirovi Ethernet okviri preko Npcapa), bez pozadinskog procesa koji se može razići s prozorom.
- **Kaže što nije u redu.** "Odgovara samo lokalni adapter", "partner ne odgovara", "tpPLC je istovremeno pokrenut", "Npcap nedostaje": prazan zaslon uvijek dolazi s razlogom.
- **Samo čita.** Adapterima postavlja samo upite, nikad ne mijenja lozinke, postavke ni firmware.
- **Bez telemetrije i računa.** Jedini mrežni zahtjev koji može poslati je "Provjeri ažuriranja", i to samo kad klikneš.

## Mogućnosti

**Vidi svoju mrežu**
- Lokalni **i udaljeni** adapteri, firmware, ID mreže i kartica preko koje je svaki viđen
- **TX / RX** brzina po vezi uživo s ocjenom kvalitete (Slabo / Prihvatljivo / Dobro / Odlično), sparkline grafikoni i **mjerač zdravlja** sa sažetkom razumljivim jezikom
- **Mapa** mreže i **graf povijesti** po vezi
- Veza koju lokalni adapter navodi, a ne odgovara, prikazuje se kao **nema veze / nedostupan**, ne kao "0 Mbps"

**Drži je na oku**
- **Auto-osvježavanje** s krugom odbrojavanja; pauza i nastavak jednim klikom
- **Obavijesti** kad adapter nestane ili se vrati, ili veza padne ispod tvog praga
- **Traka sustava**, neobavezno **pokretanje uz Windowse**, neobavezno **uvijek na vrhu**
- Neobavezni **zapis povijesti** u CSV

**Praktično**
- Preimenovanje uređaja, kopiranje MAC-a, **poredak** kartica, proširi sve, **kopiranje tekstualnog sažetka**, izvoz u **CSV / JSON**, spremanje povijesti veze u CSV
- **Paleta naredbi** (`Ctrl+K` ili `/`) i prečaci na tipkovnici
- **Dijagnostika za prijavu greške** jednim klikom (verzije, kartice, rezultati, log; MAC adrese skraćene)
- Svijetla / tamna tema, šest boja naglaska, engleski / hrvatski, opcija smanjenih animacija, responzivan raspored
- Jedna instanca: drugo pokretanje samo dovede prvi prozor naprijed
- Ugrađeno **snimanje prometa** koje omogućuje dodavanje novih modela ([CONTRIBUTING](CONTRIBUTING.md))

## Podržani hardver

| Obitelj čipova | Protokol | Status |
|---|---|---|
| Broadcom BCM60xxx (npr. **TP-Link TL-PA7017**) | EtherType `0x8912` | ✅ Testirano |
| Qualcomm Atheros (AR7400 / QCA7420 / QCA7500 …) | HomePlug AV `0x88E1` | ⚠️ Napisano prema javnoj dokumentaciji, **nije testirano na pravom uređaju** |

Ostali TP-Link modeli vjerojatno koriste jednu od te dvije obitelji, ali to je pretpostavka. Ako ga isprobaš na drugom adapteru, [otvori issue](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/issues/new/choose) i napiši što se dogodilo (gumb *Dijagnostika* u postavkama daje tekst za lijepljenje).

## Što je testirano (a što nije)

Sve niže testirala je **jedna osoba na jednom računalu**. Sve što nije u prvoj tablici smatraj neprovjerenim.

**Testno okruženje:** Windows 11 Home (build 26300), Npcap 1.89, jedan par adaptera **TP-Link TL-PA7017** (Broadcom, firmware oznaka `tpver_701E14_190426_901`), računalo spojeno na lokalni adapter preko običnog (unmanaged) switcha od 100 Mbps.

| Provjereno na gornjem pravom hardveru | |
|---|---|
| Pronalazak lokalnog i udaljenog adaptera, čitanje firmware oznake | ✅ (ponovljena skeniranja, bez sučelja i u prozoru) |
| TX / RX brzine veze | ✅ poklapaju se s tpPLC-om unutar uobičajenih oscilacija (oko 210 u tpPLC-u prema 212 i 223 u ovom alatu, čitano u različitim trenucima) |
| Novo sučelje u WebView2 na pravim adapterima: lokalni adapter, firmware, imena prenesena iz verzije 1.0 i stanje "odgovara samo lokalni adapter" | ✅ |
| Istovremeni skeneri (dvije instance skenera odjednom) | ✅ bez međusobnog smetanja |
| Klasični prozor (`--classic`) | ✅ stvarno skeniranje u 1.1; u 1.2 samo pokrenut u `--demo` načinu |

| Testirano samo s izmišljenim podacima (`--demo`), u pregledniku s demo podacima ili jediničnim testovima | |
|---|---|
| Mapa, graf povijesti, poredak, paleta naredbi, stranica postavki, boje naglaska | demo podaci |
| Pogled "veza nedostupna" (partner koji lokalni adapter navodi s 0/0 i ne odgovara) | simulirano u pregledniku; na pravoj mreži adapter je to nakratko pokazao, prije nego što je pogled postojao |
| Mreža s dva adaptera nacrtana **novim** sučeljem iz živih podataka | živo čitanje dvaju adaptera provjereno je bez sučelja; u trenutku kad je novi prozor pokrenut, udaljeni adapter nije odgovarao |
| Memorija uređaja, ponovni pokušaj, otkrivanje promjena, pohrana povijesti, datoteka postavki, CSV format, JSON stanje | jedinični testovi (70) s okvirima i vrijednostima po uzoru na snimku, ne uživo |

| **Nije testirano** | |
|---|---|
| Qualcomm Atheros adapteri (putanja koda `0x88E1`) | nikad pokrenuto na pravom hardveru |
| Bilo koji model osim TL-PA7017; više od dva adaptera na pravoj mreži | nije testirano |
| Windows 10, ARM64, drugo skaliranje zaslona, druge verzije Npcapa, računala bez WebView2 runtimea (trebalo bi prijeći na klasični prozor) | nije testirano |
| Wi-Fi adapteri | namjerno preskočeni; nije testirano |
| Obavijesti u traci, smanjivanje u traku, pokretanje uz Windowse, buđenje postojeće instance, uvijek na vrhu | nije testirano |
| Gumbi za izvoz i spremanje od početka do kraja, *Provjeri ažuriranja* prema pravom GitHub API-ju | nije ručno testirano |
| Hrvatsko sučelje u novom prozoru na hrvatskim Windowsima | viđeno samo na snimkama |
| Preuzimljiva samostalna `.exe` koju gradi GitHub Actions | autor je nije pokretao |

Primijetiš li drukčije ponašanje, [otvori issue](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/issues/new/choose).

## Instalacija

1. Instaliraj **[Npcap](https://npcap.com)** (potreban za slanje i primanje sirovih Ethernet okvira). Zadane opcije su u redu.
2. Preuzmi `Powerline-Tool-win-x64.exe` s [najnovijeg izdanja](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/releases/latest) i pokreni. .NET je ugrađen.
3. Moderno sučelje koristi **Microsoft Edge WebView2 runtime**, koji dolazi s Windowsima 11 i većinom ažuriranih Windowsa 10. Ako nedostaje, aplikacija otvara klasični prozor.
4. Ugasi tpPLC dok koristiš ovaj alat, jer dva programa koja istovremeno ispituju adaptere mogu jedan drugom smetati. Aplikacija upozori ako vidi da tpPLC radi.

Računalo spoji mrežnim kabelom na powerline adapter. Spoj preko običnog (unmanaged) switcha je u redu.

**Provjera preuzimanja** (neobavezno): svako izdanje navodi SHA-256 sažetak.

```powershell
(Get-FileHash .\Powerline-Tool-win-x64.exe -Algorithm SHA256).Hash
```

Windows SmartScreen može upozoriti na nepotpisanu aplikaciju nepoznatog izdavača; ako se sažetak poklapa, odaberi *More info → Run anyway*.

## Korištenje

| Radnja | Kako |
|---|---|
| Skeniraj odmah | **Skeniraj**, `F5` ili `R` |
| Promjena stranice | bočna traka, ili `1` `2` `3` `4` |
| Pronađi bilo koju naredbu | `Ctrl+K` ili `/` |
| Preimenuj, kopiraj MAC, povijest uređaja | na kartici uređaja |
| Postavke | bočna traka → Postavke |
| Aktivnost i log | bočna traka → Aktivnost, ili `L` |
| Pomoć s prečacima | `?` |

Prekidači naredbenog retka: `--demo` (izmišljeni uređaji), `--lang=en` ili `--lang=hr`, `--theme=light` ili `--theme=dark`, `--accent=teal|blue|violet|green|orange|pink`, `--page=devices|map|history|settings|about`, `--minimized`, `--classic`, `--devtools`, `--shot=datoteka.png` (spremi sliku prozora i izađi).

Datoteke su u `%APPDATA%\PowerlineTool`: `settings.json` (postavke i imena uređaja), `WebView2\` (profil web prikaza) i, ako je uključeno, `history.csv`.

## Rješavanje problema

**"Nije pronađen nijedan powerline adapter"** (te korake navodi i sam zaslon)
1. Je li instaliran **Npcap**?
2. Je li **tpPLC ugašen**? Zatvori ga i iz trake.
3. Je li računalo spojeno na adapter **kabelom** (ne Wi-Fi-jem) i svijetli li lampica veze?
4. Otvori ladicu aktivnosti (`L`) i klikni **Skeniraj**. Ako okviri stižu, a ništa se ne prepoznaje, tvoj model govori varijantu protokola koju ovaj alat još ne poznaje. Koristi *Snimaj promet* u postavkama i priloži datoteku uz [prijavu adaptera](https://github.com/TheFullyNormalWorkindCoder/powerline-tool/issues/new/choose).

**"Odgovara samo lokalni adapter"** Adapter utaknut u ovo računalo odgovara, ali nijedan partner ne. Provjeri je li drugi adapter uključen, uparen i u zidnoj utičnici (ne u produžnom kabelu), pa ponovno klikni Skeniraj.

**Brzine se razlikuju od tpPLC-a.** Oba čitaju brzine veze iz adaptera, a powerline brzine se iz sekunde u sekundu mijenjaju zbog električnih smetnji.

## Gradnja iz izvornog koda

Treba [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build -c Release
dotnet test  -c Release                       # parsiranje, memorija, ponovni pokušaj, promjene, povijest, postavke, stanje sučelja
dotnet run --project src/PowerlineTool -- --demo
```

Sučelje je običan HTML/CSS/JS u `src/PowerlineTool/ui` i ugrađeno je u exe. Otvoreno u običnom pregledniku (bilo koji poslužitelj statičnih datoteka) radi s ugrađenim demo podacima, pa na njemu možeš raditi bez Windowsa i hardvera. Dodaj `?theme=light&lang=hr&accent=violet&page=map` na adresu za pregled varijanti.

Jedna samostalna izvršna datoteka:

```powershell
dotnet publish src/PowerlineTool -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

Ponovno generiranje snimki zaslona: `powershell -File tools/make-screenshots.ps1`.

## Kako radi

Adapteri odgovaraju na nekoliko upravljačkih poruka poslanih kao sirovi Ethernet okviri. Broadcomov protokol je otkriven snimanjem onoga što tpPLC šalje na autorovoj vlastitoj mreži i ponavljanjem upita koji samo čitaju. Što se zna, a što je još pretpostavka, piše u **[docs/PROTOCOL.md](docs/PROTOCOL.md)**, a raspored koda i kako web sučelje razgovara s C#-om u **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** (oboje na engleskom).

## Poznata ograničenja

- Samo Windows (Npcap + WebView2/WinForms).
- Koji je adapter "lokalni" određuje se heuristikom koja se na testiranom paru poklapa s tpPLC-om, ali nije dokazana općenito.
- Brzine su prikazane kako ih adapter javlja (`vrijednost & 0x3FFF`). Na testiranom paru poklapaju se s tpPLC-om (oko 210 prema 212 Mbps), ali značenje gornjih bitova je pretpostavka.
- Partner koji nikad ne odgovori na prvo otkrivanje poznat je samo po MAC adresi koju lokalni adapter navodi, pa se prikazuje bez firmwarea.
- Nema ažuriranja firmwarea, promjene lozinke, LED-a, restarta ni štednje energije. Te naredbe nisu snimljene, a alat je namjerno samo za čitanje.
- Qualcomm putanja koda nikad nije pokrenuta na pravom adapteru.

## Komponente trećih strana

| Komponenta | Licenca | Napomena |
|---|---|---|
| [SharpPcap](https://github.com/chmorgan/sharppcap) 6.3.0 | MIT | Pristup sirovim paketima |
| [PacketDotNet](https://github.com/dotpcap/packetnet) 1.4.7 | MPL-2.0 | Dolazi uz SharpPcap; koristi se **nepromijenjen**, izvorni kod je na poveznici |
| [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2) 1.0.2903.40 | Microsoftova licenca u BSD stilu | Prikazuje sučelje; WebView2 *runtime* je komponenta Windowsa i nije ugrađen |
| .NET 8 runtime | MIT | Ugrađen u samostalnu `.exe` |
| [Npcap](https://npcap.com) | Npcap licenca | **Nije** ugrađen; instaliraš ga sam |

Samo sučelje ne koristi vanjske fontove, skripte, ikone ni slike.

## Doprinos

Testiranje na drugim modelima adaptera je najvrjednija pomoć. Vidi [CONTRIBUTING.md](CONTRIBUTING.md). Promjene su u [CHANGELOG-u](CHANGELOG.md), a sigurnosne prijave idu preko [SECURITY.md](SECURITY.md).

## Odricanje

Nije povezano s TP-Linkom, niti ga TP-Link podržava ili preporučuje. "TP-Link" i "tpPLC" su zaštitni znakovi njihovih vlasnika. Projekt ne sadrži TP-Linkov kod ni materijale. Detalji protokola uočeni su iz mrežnog prometa na autorovim vlastitim uređajima radi interoperabilnosti. Koristiš na vlastitu odgovornost.

## Licenca

[MIT](LICENSE)
