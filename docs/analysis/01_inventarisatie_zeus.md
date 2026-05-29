# OpenHPSDR Zeus - Inventarisatie Documentatie

**Project:** OpenHPSDR Zeus (EI6LF/KB2UKA)  
**Locatie:** `/mnt/data/projects/sdrapp_project/sources/openhpsdr-zeus-main/`  
**Status:** Actief ontwikkeling (v0.1 alpha, April 2026)  
**Licentiering:** GPLv2+ (dual-licensed in parts)  
**Architectuur:** .NET 10 backend + React 19 web frontend + Capacitor mobile

## 1. Projectoverzicht

### Kernkenmerken
- **Backend:** .NET 10 (C# 13.0)
- **Frontend:** React 19 + TypeScript + WebGL (Vite build)
- **Mobile:** React Native via Capacitor (Android + iOS)
- **Deployment:** Desktop app (Avalonia/WPF) OR Server-mode + browser
- **Doelborden:** Hermes Lite 2 (Protocol-1), ANAN G2/G2 MkII (Protocol-2), ANAN-100D/Angelia
- **Status Hardware Support:**
  - HL2 (P1): RX solid, TX via FM/TUNE verified
  - G2/G2 MkII (P2): RX verified, TX wired (TUNE/MOX), PureSignal converging
  - ANAN-100D (P2): RX verified, S-ATT/PRE wired
  - Hermes/ANAN/Orion: Not yet supported

### Voornaamste Karakteristieken
- Browser-gebaseerde UI (PWA capable)
- WebGL panadapter + waterfall (zoom, click-to-tune, pan)
- DSP panel: NB, NR (NR1/NR2/NR4), ANF, SNB, NBP
- Bands/modes/bandwidth/AGC/S-ATT/PRE/drive/mic gain controls
- TX: PTT, TUNE, mic uplink, TX meters, SWR-trip, TX-timeout protection
- PureSignal (P2): 4-patch convergence with AutoAttenuate
- TX Audio Tools: 10-band CFC voor voice shaping
- Live + demo S-meter met RX meter frame streaming
- Leaflet satellite map met QRZ grid-square / beam heading
- Plugin system (backend + UI + audio plugins)
- Radio discovery (P1 + P2 broadcast parallel)

---

## 2. Projectstructuur (Solutions & Assemblies)

### .NET Projecten (C# .csproj)

```
Zeus.slnx (Unified Solution)
│
├── [Backend Tier]
├── Zeus.Dsp/                          (DSP engine abstraction + WDSP bindings)
│   ├── IDspEngine.cs                  (Interface: OpenChannel, FeedIq, SetMode, etc.)
│   ├── WdspDspEngine.cs               (WDSP P/Invoke implementation)
│   ├── SyntheticDspEngine.cs          (Synthetic test implementation)
│   ├── Wdsp/WdspDspEngine.cs          (RXA/TXA P/Invoke wrappers)
│   ├── Wdsp/NativeMethods.cs          (P/Invoke signatures to wdsp.dll)
│   └── Zeus.Dsp.csproj                (Target: net10.0)
│
├── Zeus.Contracts/                    (Shared DTOs + Enums)
│   ├── MsgType.cs                     (WebSocket frame types)
│   ├── RxMode.cs                      (LSB/USB/DSB/CWL/CWU/FM/AM/DIGU/DIGL/SAM/DRM)
│   ├── HpsdrBoardKind.cs              (Enum: Hermes/HL2/G2/Angelia/etc.)
│   ├── HpsdrSampleRate.cs             (48/96/192 kHz variants)
│   ├── HpsdrAntenna.cs                (ANT1/ANT2/RX_ONLY enums)
│   ├── HpsdrAtten.cs                  (0-31 dB step attenuator enum)
│   ├── NrConfig.cs                    (NR mode + parameters struct)
│   └── Zeus.Contracts.csproj
│
├── Zeus.Protocol1/                    (Protocol-1 client implementation)
│   ├── Protocol1Client.cs             (~400 regels, UDP Metis/EP2 framing)
│   ├── Discovery/Protocol1Discovery.cs
│   ├── ITxIqSource.cs / ITxIqSource.cs (TX IQ + RX audio interfaces)
│   ├── TestToneGenerator.cs           (Tx bring-up carrier)
│   └── Zeus.Protocol1.csproj
│
├── Zeus.Protocol2/                    (Protocol-2 client implementation)
│   ├── Protocol2Client.cs             (~700 regels, UDP ETH framing)
│   ├── Discovery/Protocol2Discovery.cs
│   ├── Models/ (HiPriStatus, DdcPacket, etc.)
│   └── Zeus.Protocol2.csproj
│
├── Zeus.Plugins.Contracts/            (Plugin interface + manifest)
│   ├── IZeusPlugin.cs                 (Plugin factory interface)
│   ├── PluginManifest.cs              (Metadata: name, version, dependencies)
│   ├── IPluginRegistry.cs             (Plugin discovery/loading)
│   └── Zeus.Plugins.Contracts.csproj
│
├── Zeus.Plugins.Host/                 (Plugin loader + lifecycle)
│   ├── PluginHostService.cs
│   ├── PluginRegistry.cs
│   └── Zeus.Plugins.Host.csproj
│
├── Zeus.Server.Hosting/               (ASP.NET Core hosting layer)
│   ├── Startup.cs / Program.cs        (.NET Worker Host config)
│   ├── Controllers/ (API endpoints)
│   ├── Hubs/ (SignalR WebSocket hubs)
│   ├── Services/ (Radio/DSP service layer)
│   └── Zeus.Server.Hosting.csproj
│
├── OpenhpsdrZeus/                     (Desktop UI - Avalonia XPlatform)
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml
│   ├── Views/ (DSP panel, panadapter, meter views)
│   ├── ViewModels/ (MVVM binding)
│   └── OpenhpsdrZeus.csproj           (Target: net10.0, Avalonia 11)
│
├── [Frontend Tier]
├── zeus-web/                          (React 19 + TypeScript)
│   ├── package.json                   (React 19, Vite, TailwindCSS)
│   ├── src/
│   │   ├── App.tsx                    (Main React app)
│   │   ├── components/
│   │   │   ├── Panadapter.tsx         (WebGL spectrum/waterfall)
│   │   │   ├── Controls.tsx           (Sliders, buttons)
│   │   │   ├── Meters.tsx             (S-meter, TX meters)
│   │   │   ├── Map.tsx                (Leaflet satellite map)
│   │   │   └── ...
│   │   ├── stores/ (Zustand state management)
│   │   └── styles/ (TailwindCSS)
│   ├── index.html
│   ├── vite.config.ts
│   └── tsconfig.json
│
├── zeus-mobile/                       (React Native + Capacitor)
│   ├── package.json                   (Capacitor 6.2, React Web)
│   ├── android/ (Generated Android project)
│   ├── ios/ (Generated iOS project)
│   ├── src/ (Shared web code)
│   └── capacitor.config.ts
│
├── [Testing Tier]
├── tests/
│   ├── Zeus.Contracts.Tests/
│   ├── Zeus.Dsp.Tests/
│   ├── Zeus.Protocol1.Tests/
│   ├── Zeus.Protocol2.Tests/
│   ├── Zeus.Plugins.Contracts.Tests/
│   ├── Zeus.Plugins.Host.Tests/
│   └── Zeus.Server.Tests/
│
├── [Tooling]
├── tools/
│   ├── discovery-probe/               (Radio discovery CLI tool)
│   ├── zeus-dump/                     (Protocol frame logging)
│   └── ...
│
└── docs/                              (Wiki mirrors + diagrams)
    ├── pics/ (Screenshots)
    ├── plugins/ (Plugin author guide)
    └── api/ (OpenAPI specs)
```

---

## 3. NuGet Dependencies (Backend)

### Core Runtime
| Paket | Versie | Doel |
|---|---|---|
| System.Runtime | net10 builtin | .NET runtime |
| Microsoft.Extensions.DependencyInjection | 8.0+ | DI container |
| Microsoft.Extensions.Logging | 8.0+ | Logging abstraction |
| Microsoft.Extensions.Configuration | 8.0+ | Config management |

### Networking
| Paket | Versie | Doel |
|---|---|---|
| System.Net.Sockets | builtin | UDP/TCP |
| System.Net.NetworkInformation | builtin | Network enumeration |

### ASP.NET Core Server
| Paket | Versie | Doel |
|---|---|---|
| AspNetCore.App | net10 | ASP.NET Core framework |
| SignalR | 10.0+ | WebSocket hubs |

### Desktop/UI (Avalonia)
| Paket | Versie | Doel |
|---|---|---|
| Avalonia | 11.0+ | XPlatform UI framework |
| Avalonia.Desktop | 11.0+ | Desktop hosting |
| Avalonia.Themes.Fluent | 11.0+ | Windows Fluent theme |

### Testing
| Paket | Versie | Doel |
|---|---|---|
| xunit | 2.6+ | Unit test framework |
| Moq | 4.16+ | Mocking framework |

---

## 4. npm Dependencies (Frontend)

### Core React
```json
{
  "react": "^19.0.0",
  "react-dom": "^19.0.0"
}
```

### UI/Visualization
| Paket | Versie | Doel |
|---|---|---|
| react-grid-layout | ^2.2.3 | Draggable window layout |
| leaflet | ^1.9.4 | Map for QRZ/satellite |
| react-leaflet | ^5.0.0 | React Leaflet wrapper |
| lucide-react | ^1.11.0 | Icon library |

### State Management
| Paket | Versie | Doel |
|---|---|---|
| zustand | ^5.0.2 | Lightweight state (Redux alternative) |

### Build Tools
| Paket | Versie | Doel |
|---|---|---|
| vite | (implicit) | Fast bundler |
| typescript | ^5.7.0 | TypeScript compiler |
| tailwindcss | ^4.0.0 | Utility-first CSS |
| @tailwindcss/vite | ^4.0.0 | Tailwind Vite plugin |

### Testing
| Paket | Versie | Doel |
|---|---|---|
| vitest | (in devDeps) | Unit testing |
| @testing-library/react | (in devDeps) | React testing utils |

### Mobile (Capacitor)
```json
{
  "@capacitor/core": "^6.2.0",
  "@capacitor/android": "^6.2.0",
  "@capacitor/ios": "^6.2.0",
  "@capacitor/cli": "^6.2.0"
}
```

---

## 5. IDspEngine Interface (Volledig Gedocumenteerd)

**Bestand:** `Zeus.Dsp/IDspEngine.cs`

```csharp
public interface IDspEngine : IDisposable
{
    // Channel Lifecycle
    int OpenChannel(int sampleRateHz, int pixelWidth);
    void CloseChannel(int channelId);
    
    // RX Signal Input
    void FeedIq(int channelId, ReadOnlySpan<double> interleavedIqSamples);
    
    // Modulation & Filtering
    void SetMode(int channelId, RxMode mode);
    void SetFilter(int channelId, int lowHz, int highHz);
    void SetVfoHz(int channelId, long vfoHz);
    
    // Advanced RX
    void SetCtunShift(int channelId, int shiftHz);      // CTUN offset
    void SetAgcTop(int channelId, double topDb);        // AGC ceiling
    void SetRxAfGainDb(int channelId, double db);       // Audio gain in dB
    
    // Noise Reduction Pipeline
    void SetNoiseReduction(int channelId, NrConfig cfg);
    
    // Display Zoom
    void SetZoom(int channelId, int level);
    
    // Audio Output
    int ReadAudio(int channelId, Span<float> output);
    
    // Display Metrics (FFT/panadapter)
    bool TryGetDisplayPixels(int channelId, DisplayPixout which, 
                             Span<float> dbOut);
    
    // TX Panadapter (post-CFIR)
    bool TryGetTxDisplayPixels(DisplayPixout which, Span<float> dbOut);
    
    // PureSignal Feedback
    bool TryGetPsFeedbackPixels(DisplayPixout which, Span<float> dbOut);
    
    // TX Signal Input
    void FeedTxBlock(int txaChannelId, ReadOnlySpan<double> micSamples);
    
    // TX Audio Output
    void ReadTxIq(int txaChannelId, Span<double> interleavedIqOut);
    
    // TX Stage Meters
    bool TryGetTxMeters(Span<double> meterOut);
    
    // PureSignal Feedback Input
    void FeedPsFeedbackBlock(int psChannelId, 
                             ReadOnlySpan<double> interleavedIqFeedback);
    
    // RX Meter (S-meter)
    bool TryGetRxMeter(int channelId, out double dbm);
    
    // TX Safety
    void SetTxTimeout(int txaChannelId, int timeoutMs);
    void SetTxSwrTrip(int txaChannelId, double swrThreshold);
}

// Related Enums
public enum DisplayPixout : byte { Panadapter = 0, Waterfall = 1 }
public enum RxMode { LSB = 0, USB, DSB, CWL, CWU, FM, AM, DIGU, DIGL, SAM, DRM }

// Configuration Struct
public struct NrConfig
{
    public int Nr;                    // 0=OFF, 1=NR, 2=NR2, 3=NR4
    public int NrGainMethod;          // 0=GaussianLin, 1=GaussianLog, 2=GammaSpeech
    public int NrNpeMethod;           // 0=OSMS, 1=MMSE
    public bool NrAe;                 // Artifact Elimination
    public bool NrPost;               // Post-correction
}
```

**Implementationen:**
1. **WdspDspEngine:** Produktif (P/Invoke WDSP.dll)
2. **SyntheticDspEngine:** Geen-op testvervanging

---

## 6. Plugin Systeem Architectuur

**Bestanden:** `Zeus.Plugins.Contracts/*.cs`

### IZeusPlugin Interface
```csharp
public interface IZeusPlugin
{
    // Identification
    string Id { get; }
    string Name { get; }
    Version Version { get; }
    string Author { get; }
    
    // Lifecycle
    Task InitializeAsync(IServiceProvider services);
    Task ShutdownAsync();
    
    // Optional Hooks
    Task<object?> OnRadioConnectedAsync(IRadioClient radio);
    Task OnRadioDisconnectedAsync();
    Task OnDisplayFrameAsync(DisplayFrame frame);
}
```

### PluginManifest Format (JSON)
```json
{
  "id": "plugin.example-audio",
  "name": "Example Audio Plugin",
  "version": "1.0.0",
  "author": "Example Author",
  "description": "Demonstrates audio processing hook",
  "dependencies": [
    "zeus-contracts@>=10.0.0"
  ],
  "capabilities": [
    "audio-processing",
    "dsp-filter",
    "custom-ui"
  ],
  "entrypoint": "ExampleAudioPlugin.dll"
}
```

### Plugin Discovery & Registration
**Registry Path:** `~/.zeus-plugins/` or installation-relative  
**Auto-loading:** Manifest scan on startup, dependency resolution via NuGet-like system

**Example Plugin Architecture:**
```
audio-plugin-example/
├── Plugin.cs (implements IZeusPlugin)
├── AudioFilter.cs (audio DSP logic)
├── UI/Panel.xaml (optional Avalonia UI)
├── plugin.manifest.json
└── Plugin.csproj
```

---

## 7. Protocol 1 Implementatie (HL2 focused)

**Bestand:** `Zeus.Protocol1/Protocol1Client.cs` (~400 regels)

### Wire Format - EP2 (Endpoint 2) Rx Frame

**Frame Structure (from mi0bot HL2 protocol):**
```
Byte 0-3:     Sequence number (big-endian u32)
Byte 4:       Status/flags
Byte 5-6:     RX IQ pair 0 (12-bit I, 12-bit Q in 3 bytes)
Byte 8-9:     RX IQ pair 1
...
Byte 1030-1031: RX IQ pair 126 (last pair, 127 total @ 4 bytes each)
               [Total: 8 + 127*4 = 516 bytes payload]
```

**Sample Rate Selection (C0 byte):**
```c
enum HpsdrSampleRate {
    Rate48k = 0,
    Rate96k = 1,
    Rate192k = 2
}
```

### State Mutation (Thread-Safe)

```csharp
private long _vfoAHz = 7_100_000;                    // Frequency
private long _freqCorrectionBits = /* 1.0 bits */;   // PPM correction
private int _rate = (int)HpsdrSampleRate.Rate48k;    // Sample rate
private int _preamp;                                  // Preamp 0/1
private int _attnDb;                                  // Atten 0-31 dB
private int _mox;                                     // PTT 0/1
private int _drivePct;                                // Drive 0-100%
private int _psEnabled;                               // PureSignal arm
private int _psPredistortionValue;                    // PS predist 0-15
```

**Atomic Updates:**
- `Interlocked.Exchange()` for 64-bit frequency mutations
- `Semaphore` for TX pacing (381 packets/sec @ 48 kHz DAC)

### Network

**Socket Configuration:**
- UDP socket to radio Tx port (default 1034)
- Rx listen port (dynamic allocation or configured)
- RX timeout: 100 ms, max 10 consecutive before give-up

**TX/RX Synchronization:**
- RX thread fires incoming frame → signal TX semaphore
- TX thread paces sends to match RX rate ratio
- No external timer; radio's own clock paces transmission

---

## 8. Protocol 2 Implementatie (G2/MkII focused)

**Bestand:** `Zeus.Protocol2/Protocol2Client.cs` (~700 regels)

### Wire Format - UDP Frames

**Hi-Priority Status (broadcast UDP 1025, ≥60 bytes):**
```
+0:   Sequence (4 bytes, BE u32)
+4-19: Parsed status:
  +4: PTT / PLL lock (bit flags)
  +6: Exciter power measurement
  +10: Forward power (ALEX)
  +18: Reverse power (ALEX)
  +19: ADC levels, TX FIFO flags
```

**Receiver IQ (UDP port 1035 + DDC_ID, 1444 bytes):**
```
+0-3:  Sequence (BE u32)
+4-1443: IQ samples (238 pairs @ 4 bytes = 952 bytes)
```

**TX IQ (UDP port 1034, 1024 bytes):**
```
+0-3:  Sequence (BE u32)
+4-1023: IQ samples sent to radio
```

### Board-Specific DDC Allocation

**ANAN G2 / G2 MkII (Saturn-class):**
```
DDC 0-1: PureSignal RX (feedback channels)
DDC 2-9: User-visible receivers (default DDC 2 for single RX)
```

**Hermes (P2 mode):**
```
DDC 0-7: User-visible receivers (DDC 0 for single RX)
```

**Frequency Control:**
- Phase word: Hz × 34.952533 = 32-bit phase increment
- Ratio: 2^32 / 122_880_000 Hz (master clock)

### State Mutations (Thread-Safe)

```csharp
private uint _rxFreqHz = 14_200_000;
private int _sampleRateKhz = 48;
private byte _numAdc = 2;
private byte _rxStepAttnDb = 0;
private bool _preampOn = false;
private HpsdrBoardKind _boardKind = HpsdrBoardKind.Unknown;
private OrionMkIIVariant _variant = OrionMkIIVariant.G2;
```

### State Machine (Hi-Priority Parsing)

```
Wait for UDP 1025 → Parse sequence + meter fields
  → Update _rxMeterDbm
  → Update _txMeters (forward/reverse)
  → Update _pllLocked status
  → Broadcast StateChanged event → UI
```

---

## 9. WebSocket Frame Protocol (MsgType Enum)

**Bestand:** `Zeus.Contracts/MsgType.cs`

### Server → Client (RX & Telemetry)

| Type | Value | Payload | Frequency |
|---|---|---|---|
| DisplayFrame | 0x01 | WebGL pixels (float32 FFT bins) | 30 Hz |
| AudioPcm | 0x02 | PCM float32 samples @ 48 kHz | ~48000 Hz |
| Status | 0x03 | Mode, filter, AGC, meters | 10 Hz |
| TxMeters | 0x11 | TX stage meters (peak) | 30 Hz |
| TxStatus | 0x12 | TX safety flags (SWR, timeout) | 10 Hz |
| Alert | 0x13 | User alert message | Event-driven |
| RxMeter | 0x14 | S-meter dBm (live/demo) | 10 Hz |
| WisdomStatus | 0x15 | FFTW wisdom build state | Event-driven |
| TxMetersV2 | 0x16 | TX meters (peak + average) | 30 Hz |

### Client → Server (TX Control)

| Type | Value | Payload | Notes |
|---|---|---|---|
| MicPcm | 0x20 | 960-sample blocks @ 48 kHz | 20 ms blocks |

### Example Frame Exchange

**Client connects → receives DisplayFrame @ 30 Hz:**
```
[MsgType: 0x01][Seq: u32][Pixels: 16384 floats (4096 bins)]
→ Browser WebGL canvas renders panadapter
```

**Client sends mic audio:**
```
[MsgType: 0x20][Seq: u32][Samples: 960 floats @ 48 kHz]
→ Server TX chain consumes, generates IQ → radio
```

**Server broadcasts S-meter:**
```
[MsgType: 0x14][Seq: u32][dBm: double]
→ Browser updates needle
```

---

## 10. Audio Stack & miniaudio Integration

**Not directly visible but implied:**
- Backend uses System.Media.SoundOut or P/Invoke to platform audio
- Frontend: Web Audio API (browser context)
- Mobile: Capacitor Audio plugin

**RX Audio Path (Backend):**
```
WDSP RXA demod → AudioRing buffer → ReadAudio span
     ↓
SignalR AudioPcm frame → WebSocket → browser Web Audio
```

**TX Audio Path (Backend):**
```
WebSocket MicPcm frames → TxInputQueue
     ↓
WDSP TXA modulator ← FeedTxBlock span
     ↓
ReadTxIq span → Protocol TX IQ UDP → radio
```

---

## 11. Deployment Modi

### Desktop (Avalonia WinForms replacement)
- Single .exe (Windows) or app bundle (macOS)
- Bundled .NET runtime
- Direct hardware USB (Protocol-1)

### Server Mode (ASP.NET Core)
```bash
dotnet Zeus.Server.Hosting.dll --port 5000
# Listens HTTP:5000, serves React SPA + WebSocket hubs
```

**Browser Access:** `http://localhost:5000/`

### PWA (Progressive Web App)
- Installable from browser (add to home screen)
- Offline capability (service worker)
- Push notifications for alerts

### Mobile (Android/iOS)
- Capacitor wrapper around React web app
- Native camera/permissions (future)
- App store distribution possible

---

## 12. Test Coverage Beschrijving

**Teststructuur:**
- XUnit test projects (one per major assembly)
- Integration tests (Protocol1/Protocol2 with mocked radio)
- Performance tests (DSP pipeline throughput)

**Observed Test Paths:**
```
tests/Zeus.Contracts.Tests/
  → MsgType serialization
  → RxMode enum coverage

tests/Zeus.Dsp.Tests/
  → SyntheticDspEngine mock
  → IDspEngine contract testing

tests/Zeus.Protocol1.Tests/
  → UDP frame parsing
  → State mutation atomicity

tests/Zeus.Protocol2.Tests/
  → Hi-Pri packet parsing
  → DDC allocation logic

tests/Zeus.Plugins.Host.Tests/
  → Plugin manifest loading
  → Dependency resolution
```

**Coverage Gaps:**
- UI rendering tests (Avalonia, React) limited
- Network resilience (packet loss, latency) not visible
- PureSignal convergence algorithm not formally tested

---

## 13. Sterke Punten

1. **Modern Architecture:** .NET 10 + async/await, nullability analysis
2. **XPlatform GUI:** Avalonia unifies desktop; React unifies web/mobile
3. **WDSP Parity:** Matches Thetis DSP behavior via identical P/Invoke calls
4. **Plugin Ecosystem:** Extensible without core recompile
5. **Web-First Design:** Browser-based + PWA = no install friction
6. **TypeScript Safety:** React frontend type-checked at build time
7. **Test-Friendly:** Interface-based design (IProtocol1Client, IDspEngine) enables mocking
8. **Active Development:** Weekly releases (v0.1 era, April 2026)
9. **Community Focus:** Clear attribution (Thetis, pihpsdr, deskHPSDR sources documented)
10. **Multi-Protocol:** P1 + P2 unified interface masks protocol differences

---

## 14. Zwakke Punten

1. **Alpha Status:** Hardware validation incomplete (HL2 TX not prod-ready)
2. **Limited Board Coverage:** Only HL2 + G2 tested; Orion/Angelia future
3. **WDSP Coupling:** Tied to Windows wdsp.dll (no Linux native DSP option)
4. **Performance Unproven:** No published latency benchmarks vs Thetis
5. **Desktop Adoption:** Avalonia less mature than WinForms; fewer resources
6. **PureSignal Incomplete:** G2 MkII "converging" implies tuning loop ongoing
7. **Mobile Untested:** iOS/Android builds possible but not released
8. **Documentation Sparse:** Wiki mentions but few code comments
9. **Network Error Handling:** Silent failures on packet loss possible (no retransmit visible)
10. **Meter Calibration:** No published rig-specific meter calibration data

---

## 15. Bruikbare Referentie voor NovaSdr

**Aanbevolen Code Study:**

1. **Protocol Framing:** Protocol1/2 Client frame unpacking logic (DDC + RX IQ parsing)
2. **IDspEngine Contract:** Interface design for agnostic DSP backend swapping
3. **Plugin System:** Registry + manifest loading pattern (reusable for audio plugins)
4. **WebSocket Streaming:** MsgType frame design for real-time meter/audio over WS
5. **State Atomicity:** Interlocked.Exchange patterns for lock-free frequency updates
6. **WDSP P/Invoke:** NativeMethods.cs shows complete WDSP DLL surface (portable)
7. **UI State Management:** Zustand store pattern (lightweight Redux alternative)
8. **Deployment:** .NET app publish patterns (.exe, server, PWA variants)

**Vermijden:**
- Avalonia UI (unless building desktop; otherwise use web/React)
- Incomplete PureSignal logic (wait for v0.2 stabilization)
- Protocol-2 Orion/Angelia code (not yet verified)

---

## Conclusie

Zeus is een **modern, clean .NET reimplementation** van het HPSDR-ecosysteem met sterk focus op web + mobile accessibility. Voor NovaSdr:
- **Architecture** is voorbeeldig (dependency injection, interfaces, async)
- **DSP interface** (IDspEngine) is herbruikbaar abstractie
- **Protocol parsers** geven direct reference voor P1/P2 wire formats
- **Plugin system** kan als template dienen

**Status:** Production-ready voor HL2 RX; G2 TX experimental. Wacht op v1.0 voor productie multi-protocol deployment.

