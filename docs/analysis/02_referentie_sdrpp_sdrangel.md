# Architectuurreferentie: SDR++ en SDRangel

**Versie:** 1.0  
**Datum:** 2026-05-29  
**Doel:** Analyse van SDR++ en SDRangel als externe referentiearchitecturen voor het NovaSdr-project  
**Bronnen:** GitHub-repository AlexandreRouma/SDRPlusPlus (geraadpleegd 2026-05-29), f4exb/sdrangel (geraadpleegd 2026-05-29), directe broncode-inspectie via GitHub API

---

## 1. SDR++ — Architectuuranalyse

### 1.1 Basisgegevens

| Eigenschap | Waarde |
|-----------|--------|
| Repository | github.com/AlexandreRouma/SDRPlusPlus |
| Primaire taal | C++ (87.1%), C (11.2%) |
| Build-systeem | CMake |
| UI-framework | Dear ImGui (immediate mode GUI) |
| Licentie | GPL-3.0 |
| Stars (mei 2026) | 5970 |
| Forks | 823 |
| Laatste push | 2026-05-20 |

### 1.2 Filosofie en positionering

SDR++ positioneert zichzelf expliciet als "bloat-free SDR software". De sleutelwoorden uit de eigen README zijn: *multi VFO*, *SIMD accelerated DSP*, *cross-platform*, *modular design*. Dit is een generieke SDR-ontvanger, geen transceiver-frontend — SDR++ heeft geen TX-mogelijkheden.

### 1.3 Directory-structuur

```
SDRPlusPlus/
├── core/                    # Centrale DSP-engine, ModuleManager, config
│   └── src/
│       ├── core.h/.cpp      # Hoofd-singleton
│       ├── module.h/.cpp    # ModuleManager: laden/ontladen van .so/.dll
│       ├── signal_path/     # IQ-frontend, VFO-manager, sink, source
│       │   ├── signal_path.h
│       │   ├── iq_frontend.h
│       │   ├── vfo_manager.h
│       │   ├── source.h
│       │   └── sink.h
│       ├── dsp/             # DSP-primitieven
│       ├── gui/             # ImGui helpers en stijl
│       └── config.h/.cpp    # JSON-config (nlohmann/json)
├── source_modules/          # Hardware input drivers (28 modules)
│   ├── rtl_sdr_source/
│   ├── hackrf_source/
│   ├── airspy_source/
│   ├── hermes_source/       # OpenHPSDR Hermes!
│   ├── sdrplay_source/
│   ├── soapy_source/
│   └── ... (22 meer)
├── sink_modules/            # Audio output drivers
├── decoder_modules/         # Signaaldecoders (per plugin)
├── misc_modules/            # Utilities (freq manager, recorder, etc.)
└── src/                     # main.cpp, applicatie-entry point
```

### 1.4 Plugin/Module-architectuur

SDR++ gebruikt een **runtime-loadable shared library** aanpak. Elke module compileert naar een platform-specifieke shared library (`.dll` op Windows, `.so` op Linux, `.dylib` op macOS).

**Module-definitie** (uit `core/src/module.h`):

```cpp
// Elke module exporteert deze C-symbolen:
struct Module_t {
    HMODULE handle;                              // Platform handle
    ModuleManager::ModuleInfo_t* info;           // Metadata
    void (*init)();                              // Module-init
    ModuleManager::Instance* (*createInstance)(std::string name);
    void (*deleteInstance)(ModuleManager::Instance* instance);
    void (*end)();                               // Module-cleanup
};

// Elke module-instantie implementeert:
class Instance {
public:
    virtual ~Instance() {}
    virtual void postInit() = 0;
    virtual void enable() = 0;
    virtual void disable() = 0;
    virtual bool isEnabled() = 0;
};
```

**Module-registratie** via macro in de plugin-broncode:

```cpp
SDRPP_MOD_INFO{
    /* Name:        */ "rtl_sdr_source",
    /* Description: */ "RTL-SDR source module for SDR++",
    /* Author:      */ "Ryzerth",
    /* Version:     */ 0, 1, 0,
    /* Max instances*/ 1
};
```

Modules worden via `config.json` aan de applicatie gekoppeld — bij opstart laadt `ModuleManager` alle geconfigureerde `.so`/`.dll`-bestanden dynamisch.

**Module-typen:**

| Type | Verantwoordelijkheid | Voorbeelden |
|------|---------------------|-------------|
| Source module | Hardware-abstractie, IQ-stream leveren | rtl_sdr_source, hackrf_source, hermes_source |
| Sink module | Audio-output naar OS | audio_sink, network_sink |
| Decoder module | DSP-kanaal verwerking | wfm_radio, am_radio, ssb_demod |
| Misc module | Utilities | freq_manager, recorder, scheduler |

### 1.5 Hardware-abstractie (Device Source)

Een source module implementeert een `SourceHandler` struct die callbacks registreert bij het centrale `SignalPath`:

```cpp
// Patroon in elke source module (rtl_sdr_source als voorbeeld):
handler.ctx = this;
handler.selectHandler = menuSelected;    // Geselecteerd in UI
handler.deselectHandler = menuDeselected;
handler.menuHandler = menuHandler;       // UI-rendering (ImGui)
handler.startHandler = start;            // Start sampling
handler.stopHandler = stop;
handler.tuneHandler = tune;              // Frequentie-instelling
handler.stream = &stream;               // DSP::stream<dsp::complex_t>
```

De source levert een `dsp::stream<dsp::complex_t>` aan het `SignalPath`. Het `SignalPath` verdeelt dit IQ-signaal naar alle actieve VFO's.

**Ondersteunde hardware (source_modules/):**

```
airspy, airspyhf, audio, bladerf, dragonlabs, file,
fobossdr, hackrf, harogic, hermes (OpenHPSDR!),
hydrasdr, kcsdr, limesdr, network, perseus, plutosdr,
rfnm, rfspace, rtl_sdr, rtl_tcp, sddc, sdrplay,
sdrpp_server, soapy, spectran, spectran_http, spyserver, usrp
```

Opvallend: `hermes_source` — SDR++ ondersteunt OpenHPSDR Hermes als source. Dit is een directe overlap met NovaSdr's doelgroep.

### 1.6 DSP Pipeline

```
[Hardware source] → dsp::stream<complex_t>
    → SignalPath (IQ frontend: DC offset, IQ imbalance correctie)
        → VFO Manager (meerdere VFO's op dezelfde IQ-stream)
            → Per VFO: decimator → demodulator module
                → dsp::stream<float> (audio)
                    → Sink module (audio output)
```

DSP-kernelementen:
- **SIMD-acceleratie** via libvolk
- **FFTW3** voor spectrum-berekeningen
- **RtAudio** (Windows) / **PortAudio** (Linux/macOS) voor audio
- Eigen `dsp::stream<T>` template-klasse als thread-safe ring-buffer tussen DSP-blokken

### 1.7 Multi-Device Support

SDR++ ondersteunt **één actief device per VFO-set**, maar meerdere VFO's op hetzelfde device. Echt multi-device (twee SDR-dongles simultaan) is beperkt mogelijk via de module-architectuur maar geen primaire use case.

### 1.8 UI-architectuur

**Dear ImGui** (immediate mode): de UI-staat is niet in objecten opgeslagen maar wordt elke frame opnieuw berekend en getekend. Dit maakt de UI extreem performant maar de code minder leesbaar voor ontwikkelaars gewend aan retained-mode UI's (Qt, WinForms, React).

Voordelen:
- Geen UI-state-synchronisatie nodig
- Werkt overal waar OpenGL/DirectX beschikbaar is
- Minimale dependencies

Nadelen:
- Niet toegankelijk (geen ARIA, geen screen reader support)
- Moeilijk te themedaten
- Geen componentenbibliotheek / design system
- Niet geschikt voor complexe forms (CAT-configuratie, etc.)

### 1.9 Crossplatform aanpak

| Platform | Status | Bijzonderheden |
|---------|--------|----------------|
| Windows | Volledig ondersteund | MSVC, vcpkg voor dependencies |
| Linux | Volledig ondersteund | apt packages, .deb release |
| macOS | Volledig ondersteund | .app bundle, Homebrew |
| BSD | Build mogelijk | Geen packages |
| Android | Aanwezig in repo | `android/` directory, experimenteel |

CMake als build-systeem met platformspecifieke targets per module.

---

## 2. SDRangel — Architectuuranalyse

### 2.1 Basisgegevens

| Eigenschap | Waarde |
|-----------|--------|
| Repository | github.com/f4exb/sdrangel |
| Primaire taal | C++ (77.1%) |
| Build-systeem | CMake + CMakePresets |
| UI-framework | Qt5 + OpenGL 3.0+ |
| Licentie | GPL-3.0 |
| Stars (mei 2026) | 3797 |
| Forks | 556 |
| Laatste push | 2026-05-29 (actief!) |

### 2.2 Filosofie en positionering

SDRangel is een volwassen, feature-rijk SDR-platform met zowel RX- als TX-mogelijkheden, multi-device ondersteuning, een REST/WebSocket API, headless (server) modus en een uitgebreid plugin-ecosysteem. Het is het meest complete open-source SDR-platform in zijn klasse.

### 2.3 Directory-structuur

```
sdrangel/
├── sdrbase/                 # Core library (device, channel, DSP, plugin)
│   ├── device/              # DeviceAPI, DeviceSet, DeviceEnumerator
│   ├── channel/             # ChannelAPI, ChannelUtils
│   ├── dsp/                 # DSP-primitieven, FFT engines
│   ├── plugin/              # PluginInterface, PluginManager
│   ├── audio/               # Audio I/O abstractie
│   └── webapi/              # REST API interfaces
├── sdrgui/                  # GUI-applicatie (Qt5 hoofdvenster)
│   ├── mainwindow.cpp/h     # Hoofd UI
│   ├── device/              # Device UI management
│   ├── channel/             # Channel UI management
│   └── feature/             # Feature UI management
├── appsrv/                  # Headless server app (geen GUI)
│   └── main.cpp
├── plugins/
│   ├── samplesource/        # Hardware RX drivers (27 plugins)
│   │   ├── rtlsdr/
│   │   ├── hackrfinput/
│   │   ├── limesdrinput/
│   │   └── ... (24 meer)
│   ├── samplesink/          # Hardware TX drivers
│   ├── samplemimo/          # MIMO (RX+TX simultaan, bijv. LimeSDR)
│   ├── channelrx/           # RX demodulator plugins (35+)
│   │   ├── demodssb/
│   │   ├── demodam/
│   │   ├── wdsprx/          # WDSP-gebaseerde RX demodulator!
│   │   └── ...
│   ├── channeltx/           # TX modulator plugins
│   ├── channelmimo/         # MIMO channel plugins
│   └── feature/             # Cross-device feature plugins (19)
│       ├── rigctlserver/    # Hamlib rigctl server
│       ├── map/             # Geografische map
│       └── ...
├── devices/                 # Gedeelde hardware utility libraries
├── httpserver/              # REST API implementatie
├── ft8/                     # FT8-protocol
├── modemm17/                # M17-protocol
└── modemmeshtastic/         # Meshtastic-protocol
```

### 2.4 Plugin/Module-architectuur

SDRangel gebruikt **Qt Plugin System** (`Q_PLUGIN_METADATA`, `Q_INTERFACES`) — plugins zijn Qt-specifieke shared libraries die via Qt's plugin-loader worden ingeladen. Dit verschilt fundamenteel van SDR++'s custom `dlopen`-aanpak.

**Centrale plugin-interface** (uit `sdrbase/plugin/plugininterface.h`):

```cpp
class SDRBASE_API PluginInterface {
public:
    struct SamplingDevice {
        enum SamplingDeviceType { PhysicalDevice, BuiltInDevice };
        enum StreamType {
            StreamSingleRx,
            StreamSingleTx,
            StreamMIMO
        };
        QString displayedName;
        QString hardwareId;     // "RTLSDRInput", "HackRFInput", ...
        QString id;             // Plugin ID
        // ...
    };

    virtual const PluginDescriptor& getPluginDescriptor() const = 0;
    virtual void initPlugin(PluginAPI* pluginAPI) = 0;

    // Source plugin methoden:
    virtual void enumOriginDevices(...);
    virtual SamplingDevices enumSampleSources(...);
    virtual DeviceSampleSource* createSampleSourcePluginInstance(...);
    virtual DeviceGUI* createSampleSourcePluginInstanceGUI(...);

    // Channel plugin methoden:
    virtual ChannelAPI* createRxChannelCS(...);
    virtual ChannelGUI* createRxChannelGUI(...);
};
```

Elke plugin registreert zichzelf via Qt-macro's:

```cpp
// rtlsdrplugin.h:
class RTLSDRPlugin : public QObject, public PluginInterface {
    Q_OBJECT
    Q_INTERFACES(PluginInterface)
    Q_PLUGIN_METADATA(IID RTLSDR_DEVICE_TYPE_ID)
    // ...
};
```

### 2.5 DeviceSet model — het kernpatroon

Het `DeviceSet` is het centrale architecturale concept in SDRangel. Een `DeviceSet` representeert één hardware-device (of device-stream) met:

```cpp
class SDRBASE_API DeviceSet {
public:
    DeviceAPI *m_deviceAPI;                    // Hardware-abstractie laag
    DSPDeviceSourceEngine *m_deviceSourceEngine; // RX DSP-engine
    DSPDeviceSinkEngine *m_deviceSinkEngine;     // TX DSP-engine
    DSPDeviceMIMOEngine *m_deviceMIMOEngine;     // MIMO DSP-engine
    SpectrumVis *m_spectrumVis;                 // Spectrum visualisatie

    // Channel management:
    int getNumberOfChannels() const;
    ChannelAPI* getChannelAt(int channelIndex);
    void addChannel(ChannelAPI* channel);
    void removeChannel(ChannelAPI* channel);
};
```

**DeviceSet-hiërarchie:**

```
Application
├── DeviceSet[0]          (bijv. RTL-SDR op 144 MHz)
│   ├── DeviceAPI         (hardware interface)
│   ├── DSPDeviceSourceEngine
│   ├── SpectrumVis
│   └── Channels[]
│       ├── ChannelAPI[0] (bijv. NFM demodulator op 144.800)
│       └── ChannelAPI[1] (bijv. SSB demodulator op 144.300)
├── DeviceSet[1]          (bijv. HackRF op 433 MHz)
│   └── ...
└── Features[]            (cross-device: RigCtl, Map, ...)
```

Dit model maakt echte multi-device + multi-channel simultaan gebruik mogelijk.

### 2.6 ChannelRX Plugin: WDSP-integratie

SDRangel heeft een `wdsprx`-plugin (in `plugins/channelrx/wdsprx/`) die WDSP integreert als een RX-kanaal. Dit is bijzonder relevant voor NovaSdr.

De plugin-structuur:
- `wdsprx.h/.cpp` — ChannelAPI implementatie
- `wdsprxbaseband.h/.cpp` — Baseband verwerking via WDSP
- `wdsprxsink.h/.cpp` — Sample sink (input naar WDSP)
- `wdsprxgui.h/.cpp` — Qt UI (dialogen voor AGC, NR, NB, EQ, etc.)
- `wdsprxplugin.h/.cpp` — Plugin-registratie

Dit bewijst dat WDSP goed werkt als een kanaal-plugin in een gelaagde architectuur — exact het patroon dat NovaSdr moet volgen.

### 2.7 REST + WebSocket API

SDRangel heeft een volledige REST API geïmplementeerd via een interne HTTP server (`httpserver/`). Alle apparaat- en kanaalinstellingen zijn via API controleerbaar. Dit maakt:

- `appsrv` mogelijk: headless server, geen GUI vereist
- `sdrangelcli` mogelijk: web-applicatie die de API aanstuurt als remote control
- Integratie met externe tools (N2YO satellite tracker, etc.)

Het API-schema is gedefinieerd via Swagger/OpenAPI en gegenereerd naar C++ adapters.

### 2.8 Feature-plugins

SDRangel introduceert een derde type naast device en channel: **Feature plugins**. Features opereren cross-device en cross-channel:

| Feature plugin | Functie |
|---------------|---------|
| `rigctlserver` | Hamlib rigctld emulatie (CAT-equivalent) |
| `map` | Geografische kaart (ADS-B, AIS, APRS) |
| `satellitetracker` | Satelliettracking + automatisch tunen |
| `gs232controller` | Antennerotor controller |
| `pertester` | Protocol error rate tester |
| `ambe` | AMBE vocoder voor digitale stem |
| `demodanalyzer` | Geavanceerde signaalanalyse |

Dit concept is relevant voor NovaSdr: CAT, N1MM-integratie, Discord-integratie zijn allemaal "cross-device features" die niet aan een specifieke radio-instantie vastzitten.

### 2.9 Crossplatform aanpak

| Platform | Status | Bijzonderheden |
|---------|--------|----------------|
| Windows | Volledig | MSVC/MinGW, AppVeyor CI |
| Linux | Volledig | .deb, Flatpak, Snap, Docker |
| macOS | Volledig | Homebrew tap |
| Android | Beperkt | `androidsdrdriverinput` plugin aanwezig |

---

## 3. Vergelijkingstabel: SDR++ vs SDRangel vs Zeus

| Dimensie | SDR++ | SDRangel | Zeus (NovaSdr basis) |
|---------|-------|----------|---------------------|
| **Taal** | C++ | C++ | C# .NET 10 |
| **UI** | Dear ImGui (immediate) | Qt5 + OpenGL | React 19 + WebGL |
| **Plugin laden** | dlopen/LoadLibrary, custom | Qt Plugin System | .NET MEF / eigen IPlugin |
| **Device abstraction** | SourceHandler callbacks | DeviceAPI + DeviceSet | IRadioDevice (te definiëren) |
| **Channel model** | VFO per stream | ChannelAPI per DeviceSet | IDspEngine per device |
| **Multi-device** | Beperkt (1 actief device) | Volledig (N DeviceSets) | Gepland (niet geïmplementeerd) |
| **TX support** | Nee | Ja (samplesink plugins) | Gepland |
| **MIMO support** | Nee | Ja (samplemimo plugins) | Nee |
| **WDSP integratie** | Nee | Ja (wdsprx channel plugin) | Ja (IDspEngine → WDSP) |
| **REST/WebSocket API** | Nee | Ja (volledig Swagger) | Ja (ASP.NET Core) |
| **Headless/server mode** | Nee | Ja (appsrv) | Mogelijk via ASP.NET |
| **CAT/RigCtl** | Nee | Ja (rigctlserver feature) | Gepland (Fase 2) |
| **Cross-platform** | Win/Lin/macOS/BSD | Win/Lin/macOS | Win/Lin/macOS/mobile |
| **Mobile** | Experimenteel Android | Beperkt | Ja (Capacitor) |
| **Licentie** | GPL-3.0 | GPL-3.0 | GPL v2+ |
| **OpenHPSDR support** | hermes_source (RX only) | Nee direct | Ja (Protocol 1+2, TX+RX) |
| **Maturity** | Productierijp | Productierijp | v0.1, vroeg stadium |
| **Community** | 5970 stars | 3797 stars | Nieuw |
| **Documentatie** | Beperkt | Uitgebreid (Wiki) | Minimaal |

---

## 4. Lessons Learned: patronen die NovaSdr moet overnemen

### 4.1 DeviceSet + ChannelAPI patroon (SDRangel) — HOGE PRIORITEIT

Het `DeviceSet`-model is het meest doordachte open-source SDR-architectuurpatroon. NovaSdr moet een .NET-equivalent implementeren:

```csharp
// Equivalent in C# voor NovaSdr:
public interface IDeviceSet
{
    IDeviceEngine DeviceEngine { get; }
    ISpectrumView SpectrumView { get; }
    IReadOnlyList<IChannelPlugin> Channels { get; }
    void AddChannel(IChannelPlugin channel);
    void RemoveChannel(IChannelPlugin channel);
}
```

Dit ontwerp garandeert dat multi-RX vanaf dag één goed gemodelleerd is, ook als de implementatie pas in Fase 2 volgt.

### 4.2 Scheiding van `sdrbase` (core) en `sdrgui` (UI) (SDRangel)

SDRangel maakt een harde scheiding tussen de headless core (`sdrbase`) en de GUI (`sdrgui`). Dit is wat `appsrv` mogelijk maakt. NovaSdr's ASP.NET Core backend moet dezelfde scheiding respecteren: een `NovaSdr.Core` library die volledig testbaar en GUI-loos is, en een `NovaSdr.Desktop` applicatie die de React frontend host.

### 4.3 Plugin-metadata als declaratieve descriptor (beide projecten)

Zowel SDR++ als SDRangel laten plugins zichzelf beschrijven via metadata (naam, versie, auteur, hardware-ID). NovaSdr moet een `[PluginDescriptor]`-attribuut definiëren dat plugins declaratief annotateert:

```csharp
[PluginDescriptor(
    Id = "novasdr.device.protocol2",
    DisplayName = "OpenHPSDR Protocol 2",
    Version = "1.0.0",
    Author = "NovaSdr Project",
    DeviceType = DeviceType.Transceiver
)]
public class Protocol2DevicePlugin : IDevicePlugin { ... }
```

### 4.4 WebAPI-first design (SDRangel)

SDRangel's Swagger-gedefinieerde REST API maakt externe tooling (sdrangelcli, Python scripts, N2YO) gratis mogelijk. NovaSdr moet ASP.NET Core controllers definiëren met XML-doc comments die OpenAPI-spec genereren. Dit garandeert dat de API contractueel is en niet impliciet blijft.

### 4.5 Feature-plugins als cross-device concept (SDRangel)

CAT, N1MM-integratie, Discord, TCI zijn allemaal "features" in SDRangel-terminologie — ze opereren op applicatieniveau, niet op device- of kanaal-niveau. NovaSdr moet dit onderscheid respecteren en een `IFeaturePlugin`-interface definiëren naast `IDevicePlugin` en `IChannelPlugin`.

### 4.6 Config-persistentie via JSON met expliciet schema (SDR++)

SDR++ slaat alle module-configuratie op in een gestructureerde `config.json` met per-module secties. NovaSdr moet hetzelfde doen: één configuratie-object per plugin, serialisatie via `System.Text.Json`, schema-validatie bij laden.

---

## 5. Anti-patterns: wat NovaSdr moet vermijden

### 5.1 Dear ImGui voor complexe configuratie-UI (SDR++)

ImGui is uitstekend voor real-time data-visualisatie (spectrum, waterval) maar slecht voor complexe configuratiedialogen (CAT-setup, audio routing, filters). SDR++ heeft moeite met toegankelijkheid en complexe forms. NovaSdr's React-frontend heeft hier geen last van.

**Vermijd:** ImGui of canvas-only UI voor alles wat geen real-time visualisatie is.

### 5.2 Qt als primaire UI-toolkit voor crossplatform (SDRangel-patroon overdragen)

SDRangel's Qt5 keuze was correct voor 2015-2020, maar Qt6 licentiewijzigingen (commercieel vs. LGPL) en de opkomst van web-gebaseerde frontends maken Qt minder aantrekkelijk voor een nieuw project in 2026. Bovendien mist Qt de mobiele story die React + Capacitor biedt.

**Vermijd:** Qt als UI-framework kiezen voor NovaSdr.

### 5.3 Monolithische DSP-engine (Thetis-patroon)

Thetis' `console.cs` (53K regels) combineert UI-logica, hardware-communicatie en DSP in één klasse. SDRangel vermijdt dit door DSP-engines per device type te definiëren (`DSPDeviceSourceEngine`, `DSPDeviceSinkEngine`).

**Vermijd:** DSP-logica in UI-controllers plaatsen.

### 5.4 Plugin-systeem toevoegen na MVP (uitgesteld refactoring-risico)

Als het plugin-systeem als "Fase 2"-feature wordt beschouwd, worden alle Fase 1-implementaties direct gecoupled aan de kern. SDRangel en SDR++ definiëren hun plugin-interfaces van meet af aan.

**Vermijd:** Plugin-interfaces uitstellen tot na Fase 1. Definieer `IDevicePlugin`, `IChannelPlugin`, `IFeaturePlugin` in sprint 1 — ook als de eerste "plugin" gewoon ingebouwd is.

### 5.5 Twee UI-systemen tegelijk onderhouden (SDRangel GUI + appsrv anti-pattern)

SDRangel onderhoudt twee aparte applicatie-entrypoints (`app/` voor GUI en `appsrv/` voor headless). De code-duplicatie is beperkt maar de build-complexiteit neemt toe. NovaSdr vermijdt dit door de React-frontend altijd te hosten via ASP.NET Core — ook in "desktop"-modus draait er een lokale webserver.

**Vermijd:** Twee aparte applicatie-configuraties voor GUI en headless. Gebruik één host, optioneel zonder venster.

---

## 6. Samenvatting: architectuurpatronen voor NovaSdr

```
NovaSdr architectuurpatroon (synthese van SDR++ + SDRangel):

┌─────────────────────────────────────────────────────────────────┐
│  NovaSdr.Core (headless library)                               │
│                                                                 │
│  PluginManager                                                  │
│  ├── IDevicePlugin[]     (SDRangel: samplesource/samplesink)   │
│  ├── IChannelPlugin[]    (SDRangel: channelrx/channeltx)       │
│  └── IFeaturePlugin[]    (SDRangel: feature/)                  │
│                                                                 │
│  DeviceSetManager                                               │
│  └── IDeviceSet[]        (SDRangel: DeviceSet)                 │
│      ├── IDeviceEngine   (WDSP via IDspEngine)                 │
│      ├── ISpectrumView                                          │
│      └── IChannelPlugin[] (actieve kanalen)                    │
│                                                                 │
│  WebApiServer (ASP.NET Core)    (SDRangel: httpserver)         │
│  └── OpenAPI spec → React frontend communiceert via hier       │
└─────────────────────────────────────────────────────────────────┘
```

Van SDR++: module-metadata declaratie, per-module JSON config, SIMD DSP principes
Van SDRangel: DeviceSet model, ChannelAPI contract, Feature-plugins, WebAPI-first, sdrbase/sdrgui scheiding

---

## 7. Bibliografie

- AlexandreRouma/SDRPlusPlus, GitHub repository, geraadpleegd 2026-05-29. https://github.com/AlexandreRouma/SDRPlusPlus
- f4exb/sdrangel, GitHub repository, geraadpleegd 2026-05-29. https://github.com/f4exb/sdrangel
- SDRangel Wiki, https://github.com/f4exb/sdrangel/wiki
- SDR++ module.h, broncode directe inspectie via GitHub API, 2026-05-29
- SDRangel plugininterface.h, broncode directe inspectie via GitHub API, 2026-05-29
- SDRangel deviceset.h, broncode directe inspectie via GitHub API, 2026-05-29
- SDRangel wdsprx plugin directory, broncode directe inspectie via GitHub API, 2026-05-29
