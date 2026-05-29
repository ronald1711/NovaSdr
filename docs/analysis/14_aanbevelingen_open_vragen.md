# NovaSdr — Concrete Aanbevelingen en Open Vragen

**Versie:** 1.0  
**Datum:** 2026-05-29  
**Gebaseerd op:** Analyse van deskHPSDR (PROJECT A), Zeus (PROJECT B), Thetis (PROJECT C), SDR++ en SDRangel  
**Status:** Eerste versie, vereist validatie door het team

---

## 1. Tien concrete korte-termijn aanbevelingen

### Aanbeveling 1 — Definieer plugin-interfaces vóór Sprint 2

**Prioriteit:** Kritisch  
**Tijdslijn:** Sprint 1-2 (de eerste 4 weken)

Definieer `IDevicePlugin`, `IChannelPlugin` en `IFeaturePlugin` als publieke .NET-interfaces in een aparte `NovaSdr.Abstractions`-assembly. Publiceer deze als NuGet-package (ook lokaal via GitHub Packages). Doe dit vóór enige implementatie van Protocol 1 of Protocol 2, zodat die implementaties altijd als "plugin" worden geschreven, nooit als gecoupled core-code.

```csharp
// NovaSdr.Abstractions — minimale definitie:
public interface IDevicePlugin
{
    PluginDescriptor Descriptor { get; }
    IEnumerable<DeviceInfo> EnumerateDevices();
    IDeviceSession OpenDevice(DeviceInfo device, DeviceConfiguration config);
}

public interface IChannelPlugin
{
    PluginDescriptor Descriptor { get; }
    IChannelProcessor CreateProcessor(IChannelConfiguration config);
}

public interface IFeaturePlugin
{
    PluginDescriptor Descriptor { get; }
    void Initialize(IFeatureContext context);
}
```

**Risico bij niet-opvolging:** Alle Fase 1-implementaties worden direct gekoppeld aan de kern, wat een duur refactoring-traject in Fase 2 veroorzaakt.

---

### Aanbeveling 2 — Adopteer SDRangel's DeviceSet-model als .NET-equivalent

**Prioriteit:** Hoog  
**Tijdslijn:** Sprint 2-3

Modelleer `IDeviceSet` als centrale eenheid: één hardware-device met zijn eigen DSP-engine en een dynamische lijst van actieve kanaal-plugins. Dit maakt multi-RX architectureel correct vanaf dag één, ook als de UI het pas in Fase 2 toont.

De `DeviceSetManager` beheert de levenscyclus van alle actieve `IDeviceSet`-instanties en is de primaire interface voor de WebSocket API.

---

### Aanbeveling 3 — Implementeer de WebSocket API vóór de React-frontend

**Prioriteit:** Hoog  
**Tijdslijn:** Sprint 2-3

Definieer het WebSocket-berichtenschema (JSON-gebaseerd, bijv. via `System.Text.Json` met discriminated unions) als contract vóór de React-frontend aan de UI begint. Gebruik OpenAPI/Swagger voor de REST-endpoints. Dit voorkomt dat de frontend-developer en backend-developer in conflict raken over interface-wijzigingen.

**Minimale eerste API-endpoints:**
- `GET /api/devices` — geef lijst van beschikbare hardware
- `POST /api/devicesets` — open een DeviceSet
- `DELETE /api/devicesets/{id}` — sluit een DeviceSet
- `WS /api/spectrum/{deviceSetId}` — stream spectrumsdata
- `PUT /api/devicesets/{id}/frequency` — stel frequentie in

---

### Aanbeveling 4 — Gebruik miniaudio als enige audio-abstraction laag

**Prioriteit:** Hoog  
**Tijdslijn:** Sprint 1

Vervang elke platform-specifieke audio-aanroep (NAudio, PortAudio-sharp, etc.) door `miniaudio` via P/Invoke of een dunne C#-wrapper. miniaudio is een single-header C-bibliotheek die werkt op Windows, Linux, macOS, iOS en Android, heeft lage latency en vereist geen installatie door de eindgebruiker.

Publiceer de C#-wrapper als `NovaSdr.Audio.MiniAudio`-assembly.

---

### Aanbeveling 5 — Maak de WDSP-wrapper thread-safe en isoleer hem volledig

**Prioriteit:** Kritisch  
**Tijdslijn:** Sprint 2-4

WDSP is een C-bibliotheek met globale staat (receiver handles als integers). De `IDspEngine`-interface in Zeus verbergt dit al gedeeltelijk. Zorg dat:

1. Elke `IDeviceSet` zijn eigen WDSP-receiver-handle bezit (geen gedeelde handles)
2. Alle WDSP P/Invoke-aanroepen verlopen via een dedicated DSP-thread, nooit de UI-thread
3. De wrapper `NovaSdr.Dsp.Wdsp` geen publieke API exposeert die direct WDSP-handles lekt
4. Er unit tests zijn voor de wrapper die zonder hardware draaien (via mock IQ-data)

---

### Aanbeveling 6 — Zet Thetis' feature-inventaris om naar een gestructureerde backlog

**Prioriteit:** Middel  
**Tijdslijn:** Sprint 1 (management-taak)

Thetis (PROJECT C) is de rijkste featureset maar gearchiveerd. Maak een volledige feature-inventaris van Thetis en categoriseer elke feature als:
- MVP (blokkert vroege adopters zonder deze feature)
- Fase 2 (wenselijk voor contest/DX-gebruikers)
- Fase 3 (nice-to-have, differentiatie)
- Vervallen (Windows-only API's die niet porteerbaar zijn)

Gebruik deze inventaris als product backlog input. Zonder dit dreigt het team te vergeten dat 7K regels CAT-code en N1MM-integratie door gebruikers als vanzelfsprekend worden beschouwd.

---

### Aanbeveling 7 — Stel een licentie-compliance matrix op voor alle afhankelijkheden

**Prioriteit:** Hoog  
**Tijdslijn:** Sprint 1

NovaSdr is GPL v2+. WDSP is GPL. OpenHPSDR-protocolcode is GPL. De plugin-interfaces (`NovaSdr.Abstractions`) die community-leden zullen implementeren moeten duidelijk zijn in hun licentie-vereisten. Overweeg:

- `NovaSdr.Abstractions` (interfaces only) onder **LGPL v3** — dit maakt gesloten-source plugin-implementaties juridisch mogelijk
- `NovaSdr.Core` (implementaties) onder **GPL v2+** — behoud van copyleft
- Documenteer de keuze expliciet in `LICENSE.md` met een FAQ voor plugin-ontwikkelaars

Raadpleeg bij twijfel de FSF compatibiliteitsmatrix voor GPL v2+ vs LGPL v3.

---

### Aanbeveling 8 — Implementeer een CI/CD-pipeline voor drie platforms in Sprint 1

**Prioriteit:** Hoog  
**Tijdslijn:** Sprint 1

Stel GitHub Actions in met build-jobs voor Windows (x64), Linux (x64, Ubuntu 22.04+) en macOS (arm64 + x64). Voeg minimaal toe:
- `dotnet build` — compileert alles
- `dotnet test` — unit tests voor Core (geen hardware vereist)
- Artifacts upload (binaries als GitHub Release assets)

SDR++ en SDRangel doen dit al; het is een teken van projectvolwassenheid dat vroege adopters vertrouwen geeft.

---

### Aanbeveling 9 — Schrijf een formeel Protocol 2 conformiteitstest

**Prioriteit:** Middel  
**Tijdslijn:** Sprint 3-4

Protocol 2 (OpenHPSDR Ethernet) is complex en er bestaan subtiele implementatieverschillen tussen boards (Hermes, Angelia, Orion, Anan-60D, etc.). deskHPSDR is momenteel de referentie-implementatie. Schrijf een conformiteitstest-suite die:

1. Een simulator van Protocol 2 hardware implementeert (UDP-gebaseerd mock)
2. Alle kritieke commando's test (frequentie, sample rate, preamp, filter, TX power)
3. Draait in CI zonder echte hardware

Dit voorkomt regressies en maakt het mogelijk om nieuwe boardvarianten snel te valideren.

---

### Aanbeveling 10 — Maak een publiek "NovaSdr Architecture Decision Record" (ADR) document

**Prioriteit:** Middel  
**Tijdslijn:** Sprint 1-2

Documenteer de grote architectuurbeslissingen als ADR's in `docs/adr/`. Minimaal:
- ADR-001: Keuze voor Zeus als basis (niet greenfield)
- ADR-002: Keuze voor .NET 10 + React 19 stack
- ADR-003: Plugin-interface licentie (LGPL vs GPL)
- ADR-004: WDSP als enige DSP-bibliotheek (niet libfftw, niet custom)
- ADR-005: miniaudio als audio-abstraction

ADR's maken het mogelijk dat nieuwe bijdragers snel begrijpen waarom keuzes gemaakt zijn, zonder die discussies te heropenen.

---

## 2. Acht open vragen en onbevestigde aannames

### Open vraag 1 — Heeft Zeus een werkende Protocol 2 TX-implementatie?

**Status:** Onbevestigd  
**Context:** Zeus v0.1 claimt Protocol 1 en Protocol 2 ondersteuning. Protocol 2 TX (microfonaudio naar radio, keying, power control) is significant complexer dan RX. De analyse heeft geen TX-code bevestigd in Zeus.  
**Hoe te verifiëren:** Broncode-review van Zeus' Protocol 2 assembly; testen met echte hardware (Anan-7000DLE of equivalent); vergelijken met deskHPSDR's `radio.c` TX-implementatie.  
**Impact als niet klopt:** Protocol 2 TX moet opnieuw geïmplementeerd worden — geschatte effort 4-8 sprints.

---

### Open vraag 2 — Is WDSP thread-safe bij meerdere gelijktijdige receivers?

**Status:** Aanname (gebaseerd op deskHPSDR en Thetis gebruik)  
**Context:** Thetis ondersteunt 2 simultane receivers (PURESIGNAL vereist dit). deskHPSDR ook. Beide gebruiken aparte WDSP receiver-handles. De aanname is dat WDSP per-handle state bijhoudt en thread-safe is bij gebruik van aparte handles.  
**Hoe te verifiëren:** WDSP broncode review (wdsp.h handle management); stress-test met 2 simultane receivers in een unit test; controleer WDSP GitHub issues voor gerapporteerde race conditions.  
**Impact als niet klopt:** Multi-RX vereist mutex-synchronisatie rondom alle WDSP-aanroepen, wat latency introduceert.

---

### Open vraag 3 — Kan React 19 + WebGL een 14-bits 96kHz IQ-spectrum real-time visualiseren zonder CPU-bottleneck?

**Status:** Onbevestigd (aanname op basis van SDR++ WebGL-gebruik en Zeus' WebGL-keuze)  
**Context:** Een waterval van 96kHz breed bij 60 fps vereist ~5.7M pixels/sec update. WebGL compute shaders kunnen dit aan, maar de WebSocket-doorvoer van de C# backend naar de React frontend via localhost is de bottleneck: een FFT van 4096 punten als float32 array is 16KB per frame, 60 fps = 960KB/sec. Dit is haalbaar maar de serialisatie-overhead moet gemeten worden.  
**Hoe te verifiëren:** Bouw een prototype: C# backend genereert mock spectrum data, stuurt via WebSocket, React WebGL rendert waterfall. Meet CPU- en geheugengebruik op een middelmatige laptop (Intel Core i5, 8GB RAM).  
**Impact als niet klopt:** Binary protocol (niet JSON) nodig voor spectrum-data; of frame-rate verlagen; of server-side rendering naar afbeelding (minder interactief).

---

### Open vraag 4 — Ondersteunt Capacitor (mobile) de WebSocket/audio use case voor remote RX?

**Status:** Aanname  
**Context:** Zeus plant Capacitor voor iOS/Android. Capacitor wraps de React-webapp in een native webview. Audio via WebAudio API in een mobiele webview heeft bekende latency-problemen (iOS Safari ~100ms). Voor remote RX (luisteren op mobiel terwijl de radio thuis staat) is dit acceptabel; voor remote TX is het kritisch.  
**Hoe te verifiëren:** Bouw een Capacitor-prototype met WebAudio output op iOS 18 en Android 15. Meet audio-latency. Vergelijk met native PortAudio app.  
**Impact als niet klopt:** Mobile app beperkt tot remote control (geen audio); native audio-module nodig via Capacitor plugin (Objective-C/Swift of Java/Kotlin).

---

### Open vraag 5 — Is de Zeus GPL v2+ licentie compatible met WDSP's GPL v3?

**Status:** Kritisch risico, onbevestigd  
**Context:** Zeus claimt GPL v2+. WDSP is GPL v3 (of GPL v2+?). GPL v2 en GPL v3 zijn **niet** directe-link compatibel. Als WDSP strikt GPL v3 is en Zeus strikt GPL v2, kan Zeus WDSP niet bevatten zonder licentie-upgrade naar GPL v2+.  
**Hoe te verifiëren:** Controleer WDSP's LICENSE-bestand in de WDSP GitHub repository. Controleer deskHPSDR's licentie (die WDSP ook gebruikt). Raadpleeg de FSF compatibiliteitsmatrix.  
**Impact als niet klopt:** NovaSdr moet expliciet "GPL v2 or later" (niet "v2 only") claimen; of WDSP heeft een licentie-uitzondering nodig van de auteur (Warren Pratt, NR0V).

---

### Open vraag 6 — Heeft de OpenHPSDR-hardware-community interesse in NovaSdr als Zeus-opvolger?

**Status:** Aanname  
**Context:** Thetis is gearchiveerd. deskHPSDR-gebruikers zijn voornamelijk Linux/macOS. De OpenHPSDR community is klein maar toegewijd. Het succes van NovaSdr hangt deels af van community-adoptie en bijdragen.  
**Hoe te verifiëren:** Post een RFC (Request for Comments) op de OpenHPSDR Google Group en de Hamlib mailing list. Meet respons. Inventariseer actieve Thetis-forks (zijn mensen bezig het zelf te onderhouden?).  
**Impact als niet klopt:** NovaSdr wordt een solo/klein-team project zonder community-momentum; prioriteit van features moet scherper gesteld worden.

---

### Open vraag 7 — Ondersteunt Zeus' plugin-systeem hot-reloading van plugins zonder restart?

**Status:** Onbevestigd  
**Context:** SDR++ en SDRangel ondersteunen beide het dynamisch laden van plugins. Zeus' plugin-systeem is gedocumenteerd als aanwezig in v0.1 maar de granulariteit (statisch geladen bij startup vs. dynamisch hot-loadable) is niet bevestigd.  
**Hoe te verifiëren:** Broncode-review van Zeus' plugin-loader; test door een plugin-DLL te vervangen terwijl de applicatie draait.  
**Impact als niet klopt:** Ontwikkelcyclus voor plugin-ontwikkelaars wordt trager (plugin-ontwikkeling vereist steeds applicatie-restart). Acceptabel voor Fase 1, wenselijk te verbeteren in Fase 2.

---

### Open vraag 8 — Kan de NovaSdr WebSocket-API compatibel gemaakt worden met SDRangel's API-schema?

**Status:** Speculatief, niet onderzocht  
**Context:** SDRangel heeft een volledig Swagger-gedefinieerde REST API. Als NovaSdr een compatible (of superset) API definieert, kunnen tools als `sdrangelcli`, Python scripts en N2YO-integraties die voor SDRangel geschreven zijn ook NovaSdr aansturen.  
**Hoe te verifiëren:** Vergelijk SDRangel's Swagger spec (beschikbaar in de repo) met NovaSdr's geplande API-endpoints. Identificeer overlap.  
**Impact als klopt:** Enorme ecosysteem-voordeel gratis; SDRangel-gebruikers kunnen NovaSdr uitproberen zonder hun toolchain aan te passen.

---

## 3. Referentiebibliotheek: relevante SDR GitHub-projecten en documentatie

### 3.1 Primaire projecten (direct relevant voor NovaSdr)

| Project | URL | Relevantie |
|---------|-----|-----------|
| Zeus (OpenHPSDR) | (intern) | BASIS voor NovaSdr |
| deskHPSDR | https://github.com/g0orx/linhpsdr of g0orx fork | Protocol 1+2 referentie, WDSP integratie |
| Thetis | https://github.com/TAPR/OpenHPSDR-Thetis | Feature-inventaris referentie (gearchiveerd) |
| WDSP | https://github.com/TAPR/OpenHPSDR-wdsp | DSP-bibliotheek broncode en documentatie |

### 3.2 Architectuurreferenties (extern)

| Project | URL | Relevantie |
|---------|-----|-----------|
| SDR++ | https://github.com/AlexandreRouma/SDRPlusPlus | Plugin-model, ModuleManager, crossplatform C++ |
| SDRangel | https://github.com/f4exb/sdrangel | DeviceSet-model, ChannelAPI, WebAPI, Feature-plugins |
| SDRangel Wiki | https://github.com/f4exb/sdrangel/wiki | Architectuurdocumentatie, Quick Start |
| sdrangelcli | https://github.com/f4exb/sdrangelcli | Web-based remote control voor SDRangel (referentie voor NovaSdr remote) |
| SDRangel-Docker | https://github.com/f4exb/sdrangel-docker | Containerisatie-aanpak voor SDRangel |

### 3.3 Protocol en hardware documentatie

| Bron | URL | Relevantie |
|------|-----|-----------|
| OpenHPSDR Protocol 1 spec | https://github.com/TAPR/OpenHPSDR-Firmware | Protocol 1 Ethernet framing spec |
| OpenHPSDR Protocol 2 spec | https://github.com/TAPR/OpenHPSDR-P2 | Protocol 2 command/response spec |
| Hermes-Lite 2 | https://github.com/softerhardware/Hermes-Lite2 | Populaire open-source Protocol 2 board |
| FPGA firmware (Anan/Hermes) | https://github.com/TAPR/OpenHPSDR-Firmware | Firmware broncode voor Protocol 1/2 |

### 3.4 DSP- en audio-libraries

| Library | URL | Relevantie |
|---------|-----|-----------|
| WDSP | https://github.com/TAPR/OpenHPSDR-wdsp | Primaire DSP-bibliotheek voor NovaSdr |
| miniaudio | https://github.com/mackron/miniaudio | Crossplatform audio (NovaSdr audio-laag) |
| libvolk | https://github.com/gnuradio/volk | SIMD-vectorized DSP (optioneel, als aanvulling op WDSP) |
| liquid-dsp | https://github.com/jgaeddert/liquid-dsp | Alternatieve DSP-bibliotheek (referentie) |

### 3.5 Gerelateerde amateur-radio software (ecosysteem-context)

| Project | URL | Relevantie |
|---------|-----|-----------|
| GQRX | https://github.com/gqrx-sdr/gqrx | Qt5/GNU Radio gebaseerde SDR receiver |
| GNU Radio | https://github.com/gnuradio/gnuradio | Volledig DSP-framework (te zwaar voor NovaSdr, maar referentie) |
| Hamlib | https://github.com/Hamlib/Hamlib | CAT-protocol bibliotheek (rigctld interface) |
| WSJT-X | https://github.com/kgoba/ft8_lib | FT8/WSPR decoder (toekomstige plugin referentie) |
| Log4OM / N1MM | n.v.t. (closed source) | Logger-integraties die via UDP communiceren |
| FreeDV | https://github.com/drowe67/codec2 | Codec2 / FreeDV vocoder (digitale stem mode) |
| M17 Project | https://github.com/M17-Project | M17 digitale spraakmode (SDRangel heeft dit al) |

### 3.6 .NET / C# relevante libraries en frameworks

| Library | NuGet / URL | Relevantie |
|---------|-------------|-----------|
| System.Text.Json | (ingebouwd .NET 10) | JSON serialisatie voor plugin-config en API |
| SignalR | Microsoft.AspNetCore.SignalR | WebSocket-abstractie voor spectrum-streaming |
| MEF (Managed Extensibility Framework) | (ingebouwd .NET) | Plugin-loader, alternatief voor eigen IPlugin-systeem |
| Serilog | https://serilog.net | Structured logging voor NovaSdr backend |
| xUnit | https://xunit.net | Unit test framework (.NET) |
| Spectre.Console | https://spectreconsole.net | CLI-output voor headless/server mode logging |

### 3.7 Frontend / UI referenties

| Library/Tool | URL | Relevantie |
|-------------|-----|-----------|
| React 19 | https://react.dev | UI-framework (kern van Zeus/NovaSdr frontend) |
| Capacitor | https://capacitorjs.com | Crossplatform native wrapper (mobile) |
| WebGL / Three.js | https://threejs.org | 3D/WebGL accelerated spectrum visualisatie |
| Recharts / D3.js | https://recharts.org / https://d3js.org | Alternatieven voor spectrum-visualisatie |
| Vite | https://vitejs.dev | Frontend build tool (aanbevolen voor React 19) |
| Tauri | https://tauri.app | Alternatief voor Electron/Capacitor (Rust-based, kleiner binary) |

---

*Dit document is een levend document. Open vragen dienen geresolved te worden voor het einde van Fase 1. Aanbevelingen worden omgezet in GitHub Issues met prioriteitslabel.*
