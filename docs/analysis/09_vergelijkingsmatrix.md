# NovaSdr — Fase 9: Vergelijkingsmatrix
*Evidence-based scores | Gegenereerd: 2026-05-29*

---

## Methodologie

Scores zijn **1-10** op basis van concrete bevindingen in de broncode, architectuur, documentatie en build-systemen. Elke score heeft een korte motivatie en verwijzing naar bewijs uit de codebase.

Schaal:
- **1-3**: Ernstige tekortkomingen of volledig afwezig
- **4-6**: Aanwezig maar beperkt/verouderd
- **7-8**: Goed, voldoet aan moderne standaarden
- **9-10**: Uitstekend, best-in-class

---

## Vergelijkingstabel

| Criterium | deskHPSDR | Zeus | Thetis | Winaar |
|---|:---:|:---:|:---:|---|
| **Codekwaliteit** | 6 | **9** | 3 | Zeus |
| **Modulariteit** | 5 | **9** | 3 | Zeus |
| **Testbaarheid** | 2 | **9** | 2 | Zeus |
| **Realtime DSP performance** | **9** | 7 | 7 | deskHPSDR |
| **Audio latency** | **9** | 6 | 8 | deskHPSDR |
| **DSP volledigheid** | 8 | 8 | **9** | Thetis |
| **UI moderniteit** | 5 | **9** | 3 | Zeus |
| **Touch/mobile geschiktheid** | 2 | **8** | 1 | Zeus |
| **Crossplatform desktop** | 7 | **9** | 2 | Zeus |
| **Hardware abstractie** | 6 | 7 | 5 | Zeus |
| **Protocol 1 implementatie** | **9** | 8 | 9 | deskHPSDR/Thetis |
| **Protocol 2 implementatie** | 8 | 8 | **9** | Thetis |
| **Plugin systeem** | 3 | **9** | 5 | Zeus |
| **Maintainability** | 5 | **9** | 2 | Zeus |
| **CAT/rigctl volledigheid** | 7 | 4 | **10** | Thetis |
| **Feature volledigheid** | 7 | 5 | **9** | Thetis |
| **DX cluster/logging** | 7 | 4 | 7 | deskHPSDR/Thetis |
| **Remote operation (TCI)** | 8 | **9** | 9 | Zeus |
| **Multi-device/RX2** | 4 | 3 | 4 | Geen |
| **Documentatie kwaliteit** | 6 | **9** | 5 | Zeus |
| **Build/deploy gemak** | 6 | **9** | 5 | Zeus |
| **Licentie herbruikbaarheid** | 5 | **8** | 6 | Zeus |
| **TOTAAL (22 criteria)** | **138** | **175** | **124** | **Zeus** |

---

## Gedetailleerde Motivaties

### 1. Codekwaliteit

#### deskHPSDR — Score: 6
**Bewijs:**
- Pure C is consistent en leesbaar, met goede scheiding van concerns per `.c`/`.h` paar
- Makefile-gebaseerde build met expliciete dependency-declaraties
- Geen tests aanwezig — geen automatische verificatie van correctheid
- Sommige modules zijn groot (`old_protocol.c` 3396 regels, `new_protocol.c` 2759 regels) maar adequaat gedocumenteerd via comments
- Globale state via `radio` struct (C heeft geen betere optie)
- Kritiek: geen type-veiligheid, foutgevoelig voor buffer/pointer management

#### Zeus — Score: 9
**Bewijs:**
- C# met nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Dependency injection via ASP.NET Core `IServiceCollection`
- `IDspEngine` interface met `SyntheticDspEngine` stub — swappable implementaties
- `Zeus.Contracts` assembly: stricte scheiding van wire-format types
- `Zeus.Plugins.Contracts` assembly: versioned plugin SDK (ABI 1)
- xUnit test projecten voor elk assembly: Protocol1.Tests, Protocol2.Tests, Dsp.Tests, Contracts.Tests, Server.Tests
- Unsafe blocks enkel waar P/Invoke vereist (Zeus.Dsp, niet in applicatielogica)

#### Thetis — Score: 3
**Bewijs:**
- `console.cs` heeft **53.983 regels** — flagrant monoliet anti-pattern
- WinForms met auto-gegenereerde `console.Designer.cs` door Visual Studio — niet handmatig onderhoudbaar
- .NET 4.8 — einde Microsoft mainstream support
- SharpDX 4.2.0: **gearchiveerd in 2019**, geen bugfixes meer
- Geen test-projecten gevonden in solution
- Gearchiveerd door auteur (2 april 2026): impliciete erkenning van onhoudbaarheid

---

### 2. Modulariteit

#### deskHPSDR — Score: 5
**Bewijs:**
- Modulaire bestandsstructuur: elk feature heeft eigen `.c`/`.h` paar
- Maar: compile-time `#ifdef` flags (`#ifdef MIDI`, `#ifdef SATURN`) creëren invisible build-varianten
- Geen runtime extensibility — alles is statisch gelinkt
- `radio` struct houdt globale state samen: één globale radio-instance
- Sterk: `wdsp-1.29/` is aparte bibliotheek (`libwdsp.a`) — clean separation van DSP en app

#### Zeus — Score: 9
**Bewijs:**
- Aparte .NET assemblies per domein: Protocol1, Protocol2, Dsp, Contracts, Plugins.Contracts, Plugins.Host, Server.Hosting
- `IDspEngine` volledig decoupled: `WdspDspEngine` kan worden vervangen door `SyntheticDspEngine` zonder recompilatie
- Plugin systeem: runtime-loadable assemblies via `AssemblyLoadContext`
- React frontend is volledig decoupled van backend via WebSocket — kan vervangen worden door native app zonder DSP te wijzigen
- `zeus-web` en `zeus-mobile` zijn aparte npm packages

#### Thetis — Score: 3
**Bewijs:**
- `Console` project bevat vrijwel alles — UI, DSP-aanroepen, protocol, audio
- `CAT/`, `CW/`, `HPSDR/`, `Memory/` zijn subdirectories maar **geen aparte assemblies**
- Alles is één executable met tightly-coupled dependencies
- `Midi2Cat` en `RawInput` zijn aparte projecten (uitzondering), maar dit zijn hulptools

---

### 3. Testbaarheid

#### deskHPSDR — Score: 2
**Bewijs:**
- Geen testbestanden gevonden in repository
- `hpsdrsim.c` en `newhpsdrsim.c` zijn hardware-simulators voor handmatig testen, niet geautomatiseerd
- Pure C is testbaar via CUnit/CMocka, maar dit is niet opgezet

#### Zeus — Score: 9
**Bewijs:**
- `Zeus.Protocol1.Tests/` — protocol packet parsing unit tests
- `Zeus.Protocol2.Tests/` — protocol-2 frame parsing
- `Zeus.Dsp.Tests/` — DSP pipeline integration tests
- `Zeus.Contracts.Tests/` — wire frame serialisatie tests
- `Zeus.Server.Tests/` — TCI en endpoint integration tests
- `zeus-web/` — Vitest suites voor React state stores en audio frame encoding
- `SyntheticDspEngine` is expliciet ontworpen voor test-gebruik (geen native WDSP vereist)

#### Thetis — Score: 2
**Bewijs:**
- Geen test-projecten in `Thetis_VS2026.sln`
- Geen test-bestanden gevonden in broncode
- WinForms architectuur maakt unit-testen moeilijk (UI-state gemengd met businesslogica)

---

### 4. Realtime DSP Performance

#### deskHPSDR — Score: 9
**Bewijs:**
- Pure C — geen runtime overhead van garbage collector
- POSIX `pthread` met `SCHED_FIFO` prioriteit mogelijk (Linux)
- Directe C-functieaanroepen naar WDSP — nul P/Invoke overhead
- PortAudio op macOS: coreaudio backend met lage latency
- `CFLAGS += -O3` voor release builds — maximale compiler-optimalisatie
- FFTW3 met wisdom: vooraf berekende FFT plans

#### Zeus — Score: 7
**Bewijs:**
- .NET GC kan pauses veroorzaken; gemitigeerd via `GC.TryStartNoGCRegion()` (aanwezig in code)
- P/Invoke overhead naar WDSP: ~100-500 ns per aanroep (meting benodigd)
- `Thread.Priority = ThreadPriority.Highest` voor DSP worker thread
- `ServerGarbageCollection = false` in project file — workstation GC voor lagere pauses
- Windows timer resolution: `timeBeginPeriod(1)` voor 1ms granularity
- Zwak punt: browser WebSocket audio heeft 50-150ms extra latency (maar miniaudio native output omzeilt dit)

#### Thetis — Score: 7
**Bewijs:**
- .NET 4.8 + WinForms: ook GC-based, maar ASIO (cmASIO) biedt < 10ms audio latency
- `HiPerfTimer` class voor hoge-resolutie timing
- NAudio WASAPI exclusive mode: lage latency audio
- Zwak punt: 53K-line WinForms form vertraagt UI thread door event-flood

---

### 5. Audio Latency

#### deskHPSDR — Score: 9
**Bewijs:**
- PortAudio op macOS met CoreAudio backend: 5-10ms achievable
- PulseAudio op Linux: 20-30ms typisch
- Direct ALSA mogelijk (compile-time keuze): < 10ms
- `AUDIO=PULSE/ALSA` make.config optie

#### Zeus — Score: 6
**Bewijs:**
- miniaudio als audio backend: architectureel uitstekend (platform-native)
- Desktop (Photino.NET) met miniaudio: ~21ms (1024@48kHz) + audio buffer
- Browser mode: WebSocket audio + Web Audio API = 50-150ms extra
- Geen ASIO support (nog niet geïmplementeerd)

#### Thetis — Score: 8
**Bewijs:**
- ASIO support via `cmASIO` C++ project: < 5ms latency haalbaar
- NAudio WASAPI exclusive mode: 10-20ms
- PortAudio backup: 20-30ms
- Zwak punt: Windows-only beperkt de relevantie voor crossplatform vergelijking

---

### 6. DSP Volledigheid

#### deskHPSDR — Score: 8
**Bewijs (WDSP-1.29 features):**
- RX: AGC, ANF, NR (EMNR/RNN), NB (spectral/tone), filters, EQ (10/12-band), demodulatoren (AM/FM/SSB/CW/DIGU/DIGL)
- TX: Compressor, ALC, Leveler, CFC (10-band), PureSignal predistortion, demodulatoren
- Extra: DX cluster spectrum overlay, solar data, diversity RX
- Mist: RNN-NR4 (stubs in native/), FLDIGI native integratie

#### Zeus — Score: 8
**Bewijs:**
- Zelfde WDSP bibliotheek als deskHPSDR (identieke DSP kwaliteit)
- Aanvullend: `libspecbleach/` spectrale denoiser (aanvullend op WDSP NR)
- VST3 plugin bridge: externe DSP plugins in TX/RX chain
- NR3/NR4 stubs aanwezig voor toekomstige uitbreiding
- Mist: sommige features nog niet via UI of API exposed (vroeg stadium v0.1)

#### Thetis — Score: 9
**Bewijs:**
- Zelfde WDSP basis, maar rijkste feature-set:
- PureSignal 2.0 volledig geïmplementeerd
- Diversity RX (2 DDC's) volledig
- RNNoise (rnnoise.dll): extra NR algoritme
- SpecBleach (specbleach.dll): extra spectrale denoiser
- `clsSpectrumProcessor.cs` — aanvullende DSP voor display
- Andromeda alternative UI met eigen DSP-aanroepen
- ASIO-directe DSP path (laagste latency)

---

### 7. UI Moderniteit

#### deskHPSDR — Score: 5
**Bewijs:**
- GTK3 + Cairo: functioneel maar niet modern voor 2026 standaarden
- Cairo waterfall rendering: CPU-based, niet GPU-accelerated
- Geen docking, geen drag-and-drop panels
- Vaste window-grootte concepten
- Thema's via GTK CSS (beperkt)
- Sterk: rustige, professionele uitstraling typisch voor GTK-apps

#### Zeus — Score: 9
**Bewijs:**
- React 19: modernste web UI framework
- TailwindCSS 4: utility-first, consistent design system
- WebGL panadapter/waterfall: GPU-accelerated
- `react-grid-layout`: drag-and-drop dockable panels (`FlexWorkspace.tsx`)
- Zustand 5: minimale state management (geen Redux boilerplate)
- PWA support: offline-capable, installeerbaar als app
- Capacitor 6.2: iOS/Android mobile wrapper
- `react-leaflet`: kaartintegratie al aanwezig

#### Thetis — Score: 3
**Bewijs:**
- WinForms (2003-era technology): oud grid-layout model
- SharpDX (gearchiveerd 2019): rendering toekomst onzeker
- SkiaSharp rendering aanwezig maar beperkt gebruik
- Skin-systeem (CSS-based via ExCSS): nuttig maar complex
- Geen touch-support in WinForms (beperkt via `clsTouchHandler`)
- Windows-specifieke DPI-issues (clsDPISafeTools als workaround)

---

### 8. Touch/Mobile Geschiktheid

#### deskHPSDR — Score: 2
**Bewijs:**
- GTK3 op desktop: muisgericht, geen touch-events
- Geen adaptive layout voor tablet-schermformaten
- macOS: geen iOS support
- Raspberry Pi 5: mogelijk met touchscreen, maar UI niet geoptimaliseerd

#### Zeus — Score: 8
**Bewijs:**
- React 19: intrinsiek touch-vriendelijk via `pointer events`
- `use-pan-tune-gesture.ts`: gesture handlers voor spectrum-pan en tune
- Capacitor 6.2: native iOS en Android wrapping
- `zeus-mobile/`: aparte mobile wrapper met platform-specifieke aanpassingen
- TailwindCSS 4 responsive utilities: breakpoint-based layout
- `MobileApp.tsx`: mobile viewport detectie
- Kanttekening: audio op mobile via browser heeft hoge latency; TX op mobile is niet-triviaal

#### Thetis — Score: 1
**Bewijs:**
- WinForms heeft geen native touch-support
- `clsTouchHandler.cs` aanwezig maar primitief
- Windows-only sluit iOS/Android volledig uit
- Geen adaptive layout

---

### 9. Crossplatform Desktop

#### deskHPSDR — Score: 7
**Bewijs:**
- Linux (primair): Debian/Ubuntu/Fedora packages beschikbaar
- macOS (secundair): Homebrew build chain, native CoreAudio/CoreMIDI
- Raspberry Pi 5: community-ondersteund
- Niet: Windows (GTK3 op Windows is complex en ongebruikelijk in SDR context)
- macOS-specific code: `MacOS.c`, `macos_webview.h`, `MacTTS.h`
- macOS 26 Tahoe fix: `TAHOEFIX=ON` — actief onderhouden

#### Zeus — Score: 9
**Bewijs:**
- .NET 10: officieel ondersteund op Windows, Linux, macOS (arm64 + x64)
- `Zeus.Dsp/runtimes/`: platform-specific native libraries voor 5 platforms:
  - `linux-x64`, `linux-arm64`, `osx-arm64`, `win-x64`, `win-arm64`
- `Directory.Build.props`: gecentraliseerde build configuratie
- Photino.NET: native desktop wrapper voor Windows/Linux/macOS
- Capacitor: iOS/Android uitbreiding
- Pub profiles in OpenhpsdrZeus/Properties/PublishProfiles/

#### Thetis — Score: 2
**Bewijs:**
- `<TargetFramework>net4.8</TargetFramework>` + WinForms: Windows-only
- SharpDX: DirectX — Windows-only
- NAudio: primair Windows, Linux experimenteel
- cmASIO: Windows-only (ASIO standard is Windows)
- Geen macOS of Linux support, geen containers

---

### 10. Plugin Systeem

#### deskHPSDR — Score: 3
**Bewijs:**
- `ext.c/.h`: extensie-mechanisme aanwezig maar primitief
- TCI server (libwebsockets): externe tools kunnen via TCI koppelen
- rigctld: externe tools via rigctl protocol
- Geen runtime plugin loading
- Geen versioned plugin API
- Compile-time feature flags zijn de dichtstbijzijnde analogie van plugins

#### Zeus — Score: 9
**Bewijs:**
- `Zeus.Plugins.Contracts/IZeusPlugin.cs`: `InitializeAsync`/`ShutdownAsync` lifecycle
- `plugin.json` manifest: `schemaVersion`, `id`, `name`, `version`, `sdk.abi`, `permissions`
- `Zeus.Plugins.Host/`: `AssemblyLoadContext`-based dynamische loading
- Extension interfaces: `IBackendPlugin`, `IUiPlugin`, `IAudioPlugin`
- VST3 bridge: externe VST3 plugins in TX/RX audio chain
- Registry: officiële plugin repository + URL-based installatie
- `zeus-web/src/plugins/`: client-side plugin runtime

#### Thetis — Score: 5
**Bewijs:**
- `Microsoft.CodeAnalysis` scripting engine: runtime C# compilation — uniek en krachtig
- `frmMacroButtonConfig.cs`: macro-systeem (workflow automation)
- Geen formele plugin API of manifest format
- TCI server, CAT server, N1MM als impliciete extensie-punten
- Discord.Net bot: ongewone maar innovatieve integratie

---

### 11. CAT/rigctl Volledigheid

#### deskHPSDR — Score: 7
**Bewijs:**
- `rigctl.c` (5588 regels): CAT/Hamlib emulatie
- TS2000 en PowerSDR modi ondersteund
- TCP port-based server
- Hamlib rigctld integratie
- `build-rigctld.sh`: standalone rigctld builder

#### Zeus — Score: 4
**Bewijs:**
- TCI server: uitgebreid (3357 regels)
- Beperkte CAT implementatie (nog niet volledig gedocumenteerd in v0.1)
- Geen expliciete Kenwood-compatibele CAT server gevonden

#### Thetis — Score: 10
**Bewijs:**
- `CATCommands.cs` (7000+ regels): volledige Kenwood TS-commando set
- `CATParser.cs` (1200 regels): syntax parser
- `CATStructs.xml` (79 KB): XML command definities
- `SDRSerialPortII.cs` (63 KB): seriële COM port CAT server
- `TCPIPcatServer.cs` (25 KB): TCP/IP CAT server
- ICOM, Yaesu, Kenwood varianten ondersteund
- Extended commands voor HPSDR-specifieke functies

---

### 12. Remote Operation

#### deskHPSDR — Score: 8
**Bewijs:**
- TCI server (libwebsockets): volledig geïmplementeerd
- rigctld TCP: remote CAT
- STEMLAB HTTP: remote radio via web interface
- `newhpsdrsim.c`: simulator voor remote testing

#### Zeus — Score: 9
**Bewijs:**
- Service mode: headless HTTP/HTTPS server (`--service` arg)
- Server mode: service + status window (`--server` arg)
- Desktop mode: Photino.NET lokale webview (`--desktop` arg)
- REST API: volledig remote control
- WebSocket streaming: real-time spectrum/audio/meters via netwerk
- PWA: installeerbaar remote client via browser
- TCI server: externe tools koppelen
- HTTPS: self-signed LAN cert

#### Thetis — Score: 9
**Bewijs:**
- TCI server: 1000 regels
- CAT via TCP
- N1MM UDP streaming
- Hamlib/rigctld compatible
- Discord bot: remote notificaties
- RDP-bruikbaar (WinForms op Windows Remote Desktop)

---

## Samenvatting per Categorie

### Winnaar per domein

| Domein | Winnaar | Reden |
|---|---|---|
| Architectuurkwaliteit | **Zeus** | Moderne .NET 10, clean interfaces, tests |
| Realtime DSP | **deskHPSDR** | Native C, geen GC, directe hardware |
| Feature volledigheid | **Thetis** | Meest functies geïmplementeerd |
| UI moderniteit | **Zeus** | React + WebGL + PWA + mobile |
| Protocol diepte | **Thetis** | Meest complete P1/P2 implementatie |
| Crossplatform | **Zeus** | Windows + Linux + macOS + mobile |
| CAT/integratie | **Thetis** | 7000-line Kenwood implementatie |
| Plugin systeem | **Zeus** | Versioned SDK, manifest, VST3 |
| Remote operation | **Zeus** | Service mode, REST API, WebSocket |
| Maintainability | **Zeus** | .NET 10, tests, separation of concerns |

---

## Conclusie: NovaSdr = Best-of-Three

```
NovaSdr architectuur:     Zeus (basis)
NovaSdr protocol refs:    deskHPSDR (edge cases, Saturn) + Thetis (P1/P2 diepte)
NovaSdr feature refs:     Thetis (CAT, N1MM, Discord, CW features)
NovaSdr DSP:              WDSP (gemeenschappelijk) + Zeus IDspEngine interface
NovaSdr audio:            miniaudio (Zeus) + optionele ASIO plugin (Thetis model)
NovaSdr UI:               React 19 + WebGL (Zeus) + HamDash-geïnspireerde panels
```
