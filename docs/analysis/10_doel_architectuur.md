# NovaSdr — Fase 10: Doelarchitectuur
*Architectuurontwerp voor de nieuwe SDR RX/TX applicatie | Gegenereerd: 2026-05-29*

---

## Overzicht

De NovaSdr doelarchitectuur is gebaseerd op **OpenHPSDR-Zeus als fundament**, uitgebreid met een volwaardige hardware abstraction layer, multi-device support, en de rijke feature-set geïnspireerd door deskHPSDR en Thetis.

---

## 10.1 Architectuurprincipes

1. **Separation of concerns:** Elk domein heeft een eigen laag, geen laag-doorkruisende afhankelijkheden
2. **Testbaarheid first:** Elke laag heeft een interface die vervangbaar is door een test-stub
3. **Protocol agnostisch:** Nieuwe hardware wordt toegevoegd via een nieuwe adapter, niet via aanpassing van bestaande code
4. **Feature incrementeel:** MVP werkt met Brick2 + RTL-SDR; elk volgend device wordt via dezelfde interface toegevoegd
5. **Realtime-safe DSP:** DSP-threads draaien op hoogste prioriteit, geen allocaties in hot path
6. **UI decoupled:** Frontend communiceert uitsluitend via REST/WebSocket — kan vervangen worden zonder DSP te wijzigen

---

## 10.2 Negen-Laags Architectuurdiagram

```
╔══════════════════════════════════════════════════════════════════════════════╗
║                       NOVASDR — 9-LAAGS ARCHITECTUUR                        ║
╠══════════════════════════════════════════════════════════════════════════════╣
║                                                                              ║
║  ┌──────────────────────────────────────────────────────────────────────┐   ║
║  │  LAAG 1: PRESENTATION / UI                                           │   ║
║  │                                                                       │   ║
║  │  Browser/PWA        Desktop (Photino.NET)    Mobile (Capacitor 6)    │   ║
║  │  ┌──────────┐       ┌───────────────────┐   ┌─────────────────────┐  │   ║
║  │  │React 19  │       │Photino.NET        │   │Capacitor Android    │  │   ║
║  │  │+WebGL    │       │wraps same React   │   │Capacitor iOS        │  │   ║
║  │  │+Zustand 5│       │zonder browser     │   │wraps same React     │  │   ║
║  │  │+TailwindCSS4    │audio latency      │   │touch-optimized      │  │   ║
║  │  │+react-grid│      └───────────────────┘   └─────────────────────┘  │   ║
║  │  │+react-leaflet                                                      │   ║
║  │  │+lucide-react                                                       │   ║
║  │  └──────────┘                                                         │   ║
║  │                                                                       │   ║
║  │  Key components:                                                      │   ║
║  │  • PrimarySpectrumPanel — WebGL panadapter + waterfall               │   ║
║  │  • Rx2SpectrumPanel — WebGL spectrum voor aux device                 │   ║
║  │  • VfoPanel — VFO-A/B, mode, filter, AGC controls                   │   ║
║  │  • MetersPanel — S-meter, TX meters (fwd/ref/SWR)                   │   ║
║  │  • BandAwarenessPanel — DX spots, propagatie, maps                  │   ║
║  │  • DeviceManagerPanel — discovery en device koppeling               │   ║
║  │  • FlexWorkspace — dockable panels (react-grid-layout)              │   ║
║  └──────────────────────────────┬───────────────────────────────────────┘   ║
║                                  │ REST + SignalR WebSocket                   ║
║  ┌───────────────────────────────▼──────────────────────────────────────┐   ║
║  │  LAAG 2: APPLICATION SERVICES (ASP.NET Core .NET 10)                │   ║
║  │                                                                       │   ║
║  │  DspPipelineService      Rx2PipelineService    DeviceCoordinator     │   ║
║  │  TxService               TxAudioIngest         DiscoveryAggregator   │   ║
║  │  CwEngine                MorseEncoder           StreamingHub (SignalR)│   ║
║  │  TciServer               BandPlanService        RadioCalibrations     │   ║
║  │  AudioPluginBridge       PsAutoAttenuate        ZeusEndpoints (REST)  │   ║
║  └───────────────────────────────┬──────────────────────────────────────┘   ║
║                                  │                                            ║
║  ┌───────────────────────────────▼──────────────────────────────────────┐   ║
║  │  LAAG 3: SDR DOMAIN LAYER                                            │   ║
║  │                                                                       │   ║
║  │  RadioSession                                                         │   ║
║  │  ├── PrimaryTransceiver (VFO-A, VFO-B, TxState, BandManager)       │   ║
║  │  └── AuxiliaryReceivers[] (VfoState, FreqSyncPolicy, AudioRoute)    │   ║
║  │                                                                       │   ║
║  │  VfoManager  BandManager  ModeManager  PttController  BandStack      │   ║
║  │  FreqSyncPolicy  AudioRoutingPolicy  DeviceCoordinatorService        │   ║
║  └───────────────────────────────┬──────────────────────────────────────┘   ║
║                                  │                                            ║
║  ┌───────────────────────────────▼──────────────────────────────────────┐   ║
║  │  LAAG 4: DSP / AUDIO ENGINE                                          │   ║
║  │                                                                       │   ║
║  │  IDspEngine interface                                                 │   ║
║  │  ├── WdspDspEngine (primary: channels 0-13 + TX ch.14)              │   ║
║  │  └── WdspDspEngine (aux: channels 16-31 voor RX2 devices)           │   ║
║  │                                                                       │   ║
║  │  IAudioRouter ──► SpscRing<float> ──► miniaudio output              │   ║
║  │  ISpectrumTap ──► WebGL canvas (primary) + WebGL canvas (RX2)       │   ║
║  │  SampleRateBridge ──► decimatie van native device rate → 48 kHz     │   ║
║  │                                                                       │   ║
║  │  native/wdsp/  — WDSP library (Warren Pratt NR0V, GPL v2+)          │   ║
║  │  native/miniaudio/ — cross-platform audio (David Reid)              │   ║
║  │  native/zeus-vst-bridge/ — VST3 plugin host                         │   ║
║  │  native/libspecbleach/ — spectrale denoiser                         │   ║
║  └───────────────────────────────┬──────────────────────────────────────┘   ║
║                                  │                                            ║
║  ┌───────────────────────────────▼──────────────────────────────────────┐   ║
║  │  LAAG 5: HARDWARE ABSTRACTION LAYER (HAL)                            │   ║
║  │                                                                       │   ║
║  │  ITransceiver (TX + RX capable)      IDeviceSource (RX only)         │   ║
║  │  DeviceRegistry                       CapabilityModel                 │   ║
║  │  WdspChannelAllocator                 DeviceProfileStore              │   ║
║  │  StationProfile                       AuxiliaryReceiver model         │   ║
║  └────┬──────────────────────────────────────────────────────────────┬──┘   ║
║       │ Protocol adapters                                             │       ║
║  ┌────▼──────────────────────────────────────────────────────────────▼──┐   ║
║  │  LAAG 6: PROTOCOL ADAPTERS                                           │   ║
║  │                                                                       │   ║
║  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────┐│   ║
║  │  │OpenHpsdrP1   │  │OpenHpsdrP2   │  │SoapySdrSource│  │Sdrplay   ││   ║
║  │  │Transceiver   │  │Transceiver   │  │(RX-only via  │  │Source    ││   ║
║  │  │(Zeus P1 ✓)  │  │(Zeus P2 ✓)  │  │SoapySDR.so)  │  │(API 3.x) ││   ║
║  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────┘│   ║
║  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │   ║
║  │  │RtlSdrSource  │  │PlutoSdrSource│  │SaturnExtended│              │   ║
║  │  │(librtlsdr    │  │(libiio       │  │Transceiver   │              │   ║
║  │  │ P/Invoke)    │  │ P/Invoke)    │  │(P2 + Saturn) │              │   ║
║  │  └──────────────┘  └──────────────┘  └──────────────┘              │   ║
║  └───────────────────────────────────────────────────────────────────────┘   ║
║                                                                              ║
║  ┌───────────────────────────────────────────────────────────────────────┐   ║
║  │  LAAG 7: PLUGIN / INTEGRATION LAYER                                  │   ║
║  │                                                                       │   ║
║  │  IZeusPlugin lifecycle (InitAsync / ShutdownAsync)                   │   ║
║  │  IBackendPlugin  IUiPlugin  IAudioPlugin                              │   ║
║  │  plugin.json manifest (ABI versioned)                                │   ║
║  │                                                                       │   ║
║  │  Core plugins (meegeleverd):                                         │   ║
║  │  • TCI Server          • CW Keyer Engine                             │   ║
║  │  • CAT/Hamlib plugin   • Audio VST3 bridge                           │   ║
║  │                                                                       │   ║
║  │  Ecosystem plugins (optioneel):                                      │   ║
║  │  • N1MM UDP streaming  • DX Cluster telnet                           │   ║
║  │  • Solar/greyline      • PSK Reporter                                │   ║
║  │  • SDRplay native API  • Discord bot                                 │   ║
║  └───────────────────────────────────────────────────────────────────────┘   ║
║                                                                              ║
║  ┌────────────────────────────┐  ┌──────────────────────────────────────┐   ║
║  │  LAAG 8: PERSISTENCE       │  │  LAAG 9: TELEMETRY                   │   ║
║  │                            │  │                                       │   ║
║  │  LiteDB embedded NoSQL     │  │  Serilog structured logging           │   ║
║  │  • DeviceProfileStore      │  │  OpenTelemetry traces                 │   ║
║  │  • BandMemoryStore         │  │  /api/diagnostics/latency endpoint    │   ║
║  │  • FilterPresetStore       │  │  DSP pipeline timing probes           │   ║
║  │  • StationProfileStore     │  │  GC pause monitoring                  │   ║
║  │  • Rx2DeviceStore (new)    │  │  Per-device telemetry                 │   ║
║  │  • MultiDeviceLayoutStore  │  │                                       │   ║
║  └────────────────────────────┘  └──────────────────────────────────────┘   ║
║                                                                              ║
║  HARDWARE:  [Brick2/P1] [Brick2/P2] [Saturn] [SDRplay] [RTL-SDR] [Pluto]   ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

---

## 10.3 Laag 1: Presentation / UI

### Verantwoordelijkheden
- Visualisatie van spectrum, waterfall, VFO, meters, band awareness
- Gebruikersinvoer verwerken (frequentie, mode, filter, PTT, gain)
- Multi-device layout beheren
- PWA offline support
- Mobile/tablet adaptieve layout

### Technologieën (hergebruik uit Zeus)
- **React 19** + Vite 6 + TypeScript 5.7
- **TailwindCSS 4** — utility-first, consistent design system
- **WebGL** — GPU-accelerated panadapter/waterfall (`zeus-web/src/gl/`)
- **Zustand 5** — per-domain state stores (radio, display, meters, layout, devices)
- **react-grid-layout** — draggable panels (`FlexWorkspace.tsx`)
- **react-leaflet** — kaartintegratie (DX spots, greyline)
- **Capacitor 6.2** — iOS/Android mobile wrapper
- **Photino.NET** — desktop wrapper (omzeilt browser audio latency)

### Nieuwe panels voor NovaSdr
```typescript
// MultiDevicePanel.tsx — primary + RX2 side-by-side spectrum
// DeviceManagerPanel.tsx — discovery en device koppeling
// AuxiliaryReceiverPanel.tsx — per-aux VFO/gain/mode controls
// BandAwarenessPanel.tsx — DX spots + propagatie (HamDash-inspired)
// StationProfilePanel.tsx — multi-device sessies opslaan/laden
```

### Waarom beter dan de bestaande projecten
- deskHPSDR: GTK3 Cairo is niet GPU-accelerated, geen docking, geen mobile
- Thetis: WinForms 2003-era, SharpDX archived, Windows-only
- Zeus basis: al uitstekend, uitbreiden met multi-device panels

---

## 10.4 Laag 2: Application Services

### Verantwoordelijkheden
- Orchestratie van DSP pipeline
- TX audio ingest en routing
- Device coordinator (PTT lockout, frequentiesync)
- WebSocket streaming hub
- REST API endpoints
- TCI server
- CW engine

### Technologieën (hergebruik uit Zeus)
- **ASP.NET Core .NET 10** — service hosting
- **SignalR** — binary WebSocket protocol (displayframes, audio, meters)
- **Microsoft.Extensions.DependencyInjection** — DI container
- **BackgroundService** — long-running services

### Nieuwe services voor NovaSdr
```csharp
// Rx2PipelineService : BackgroundService
//   Analoog aan DspPipelineService maar voor auxiliary device
//   Eigen WDSP channel pool (16-31)
//   Eigen SignalR frame type voor RX2 spectrum/meters

// DeviceCoordinatorService : BackgroundService
//   PTT lockout enforcement
//   Frequentiesynchronisatie
//   Audio routing arbitratie

// DiscoveryAggregatorService
//   Combineert OpenHPSDR discovery + SoapySDR enumerate + IIO scan
//   Deduplicatie en conflict detectie
```

---

## 10.5 Laag 3: SDR Domain Layer

### Verantwoordelijkheden
- Radiostatus beheren (frequentie, mode, filter, PTT)
- Band management (band memories, bandstack)
- Multi-device sessie model
- Frequentiesynchronisatie policy

### Kernentiteiten

```csharp
public sealed class RadioSession : IAsyncDisposable
{
    public ITransceiver PrimaryTransceiver { get; }
    public IReadOnlyList<AuxiliaryReceiver> AuxiliaryReceivers { get; }
    public bool IsMoxActive { get; }
    public string ProfileId { get; set; }  // actief station profiel

    public Task AddAuxiliaryAsync(IDeviceSource device, AuxReceiverConfig config);
    public Task RemoveAuxiliaryAsync(string deviceId);
}

public sealed class AuxiliaryReceiver
{
    public IDeviceSource Device { get; }
    public VfoState Vfo { get; set; }
    public FreqSyncPolicy FreqSync { get; set; }
    public AudioRoutePolicy AudioRoute { get; set; }
    public int WdspChannelId { get; }  // 16-31
}

public record VfoState
{
    public long FrequencyHz { get; init; }
    public RxMode Mode { get; init; }
    public FilterPreset Filter { get; init; }
    public int AgcMode { get; init; }
    public string? SyncSourceDeviceId { get; init; }
    public long SyncOffsetHz { get; init; }
}
```

---

## 10.6 Laag 4: DSP / Audio Engine

### Verantwoordelijkheden
- WDSP IQ verwerking (RXA + TXA chains)
- Audio routing (primary → miniaudio output)
- Spectrum pixel generatie (→ WebGL canvas)
- Sample rate bridging voor aux devices

### IDspEngine interface (volledig hergebruiken uit Zeus)

```csharp
public interface IDspEngine
{
    // Channel lifecycle
    int OpenChannel(int sampleRateHz, int pixelWidth);
    void CloseChannel(int channelId);

    // RX chain
    void FeedIq(int channelId, ReadOnlySpan<double> interleavedIq);
    void SetMode(int channelId, RxMode mode);
    void SetFilter(int channelId, int lowHz, int highHz);
    void SetVfoHz(int channelId, long vfoHz);
    void SetAgcTop(int channelId, double topDb);
    void SetRxAfGainDb(int channelId, double db);
    void SetNoiseReduction(int channelId, NrConfig cfg);
    int ReadAudio(int channelId, Span<float> output);
    bool TryGetDisplayPixels(int channelId, DisplayPixout which, Span<float> dbOut);

    // TX chain
    int OpenTxChannel(int outputRateHz = 48_000);
    void SetMox(bool moxOn);
    bool TryGetTxDisplayPixels(DisplayPixout which, Span<float> dbOut);

    // Metering
    double GetRxSignalDb(int channelId);
    double GetRxAdcPeakDb(int channelId);
    double GetTxFwdWatts();
    double GetTxRevWatts();
    double GetTxSwr();
}
```

### Thread model

```
NETWORK THREAD (per device)
    UDP ontvangen → IqBlock → BlockingCollection<IqBlock>
    Priority: AboveNormal
    ↓
SAMPLE RATE BRIDGE (per aux device)
    IqBlock @ native rate → IqBlock @ 48 kHz
    Priority: Normal
    ↓
DSP WORKER THREAD (per WDSP channel)
    fexchange0() loop, 1024 samples @ 48 kHz
    Priority: Highest
    GC.TryStartNoGCRegion() voor realtime segmenten
    ↓
PIPELINE TICK (30 Hz timer)
    Audio drain → IAudioRouter → SpscRing
    Display pixels → SignalR hub push
    Meters → SignalR hub push
    ↓
AUDIO CALLBACK (miniaudio, per device)
    SpscRing → audio output callback
    Priority: Highest (audio thread pinned)
    Lock-free SpscRing als bridge
```

### WDSP Channel Partitionering

```
Channels 0-7:   Primary RX (VFO-A, VFO-B, en future DDCs)
Channel 14:     Primary TX (één per sessie)
Channels 16-29: Auxiliary RX (max 14 aux devices)
Channels 30-31: Gereserveerd
```

---

## 10.7 Laag 5: Hardware Abstraction Layer

### Capability-based device model

```csharp
[Flags]
public enum DeviceCapabilities
{
    None            = 0,
    Receive         = 1 << 0,
    Transmit        = 1 << 1,
    FullDuplex      = 1 << 2,
    DualRx          = 1 << 3,
    PureSignal      = 1 << 4,
    VariableRate    = 1 << 5,
    HardwareAtt     = 1 << 6,
    DiversityRx     = 1 << 7,
    HwAGC           = 1 << 8,
    BiasTee         = 1 << 9,
    DirectSample    = 1 << 10,
    WideFreq        = 1 << 11,
}

public interface IDeviceSource : IAsyncDisposable
{
    string DeviceId { get; }
    string FriendlyName { get; }
    DeviceCapabilities Capabilities { get; }
    FrequencyRange[] SupportedRanges { get; }
    int[] SupportedSampleRates { get; }

    Task<bool> OpenAsync(DeviceOpenOptions opts, CancellationToken ct);
    IAsyncEnumerable<IqBlock> StreamAsync(CancellationToken ct);
    Task SetFrequencyAsync(long hz, CancellationToken ct);
    Task SetGainAsync(double db, CancellationToken ct);
    Task SetSampleRateAsync(int hz, CancellationToken ct);
}

public interface ITransceiver : IDeviceSource
{
    Task SetMoxAsync(bool keyed, CancellationToken ct);
    Task<bool> SetTxFrequencyAsync(long hz, CancellationToken ct);
    IAsyncEnumerable<TxFeedbackBlock> TxFeedbackAsync(CancellationToken ct);
}
```

### UI capability adaptatie

```typescript
// React: verberg TX controls als device geen TX heeft
const showTxControls = device.capabilities & DeviceCapabilities.Transmit;
const showPsButton = device.capabilities & DeviceCapabilities.PureSignal;
const showBiasTee = device.capabilities & DeviceCapabilities.BiasTee;
```

---

## 10.8 Laag 6: Protocol Adapters

| Adapter | Interface | Backend | Status |
|---|---|---|---|
| `OpenHpsdrP1Transceiver` | `ITransceiver` | Zeus.Protocol1 (volledig hergebruiken) | MVP ✓ |
| `OpenHpsdrP2Transceiver` | `ITransceiver` | Zeus.Protocol2 (volledig hergebruiken) | MVP ✓ |
| `RtlSdrSource` | `IDeviceSource` | librtlsdr P/Invoke | MVP ✓ |
| `SoapySdrSource` | `IDeviceSource` | SoapySDR P/Invoke | Fase 1 |
| `SdrplaySource` | `IDeviceSource` | SDRplay API 3.x P/Invoke (opt. plugin) | Fase 2 |
| `PlutoSdrSource` | `IDeviceSource` | libiio P/Invoke | Fase 2 |
| `PlutoSdrTransceiver` | `ITransceiver` | libiio full-duplex | Fase 2 |
| `SaturnExtendedTransceiver` | `ITransceiver` | P2 + Saturn-specific registers | Fase 2 |

---

## 10.9 Laag 7: Plugin / Integration Layer

### Plugin categorieen

**Core plugins (meegeleverd in NovaSdr distributie):**

| Plugin | Basis | Implementatie |
|---|---|---|
| TCI Server | Zeus `Tci/` (3357 lines) | Volledig hergebruiken, uitbreiden met RX2 |
| CW Keyer | Zeus `CwEngine.cs` | Volledig hergebruiken |
| CAT/Hamlib | Thetis `CATCommands.cs` (referentie) | Nieuw C# plugin op basis van referentie |
| VST3 Audio | Zeus `zeus-vst-bridge/` | Volledig hergebruiken |

**Ecosystem plugins (optioneel, apart installeerbaar):**

| Plugin | Basis | Status |
|---|---|---|
| N1MM UDP | Thetis `N1MM.cs` (model) | Fase 2 |
| DX Cluster | deskHPSDR `dxcluster.c` (model) | Fase 3 |
| Solar/Greyline | deskHPSDR `libsolar/` (model) | Fase 3 |
| Discord Bot | Thetis `clsDiscord.cs` (model) | Fase 3 |
| SDRplay Native | SDRplay API (proprietary) | Fase 2 (binary-only plugin) |
| PSK Reporter | Nieuw | Fase 3 |
| WSJT-X VAC | Nieuw (IPC/VAC bridge) | Fase 2 |

### Plugin ABI uitbreidingen

```csharp
// Uitbreiding van bestaande Zeus PluginCapabilities enum:
public enum PluginCapabilities : long
{
    // Bestaand (Zeus):
    PersistSettings = 1 << 0,
    ControlRadio    = 1 << 1,
    AccessAudio     = 1 << 2,
    AccessDisplay   = 1 << 3,
    AccessMeters    = 1 << 4,

    // Nieuw voor NovaSdr multi-device:
    ReceiveRx2Stream  = 1 << 8,
    ControlRx2        = 1 << 9,
    AccessHardware    = 1 << 10,  // directe IDeviceSource toegang
    StreamN1mmUdp     = 1 << 11,
    DxClusterConnect  = 1 << 12,
}
```

---

## 10.10 Laag 8: Persistence / Configuration

### LiteDB stores (uitbreiding van Zeus)

```
Bestaand (volledig hergebruiken):
✓ RadioStateStore
✓ DspSettingsStore
✓ FilterPresetStore
✓ BandMemoryStore
✓ BandPlanStore
✓ DisplaySettingsStore
✓ PsSettingsStore
✓ LayoutStore

Nieuw voor NovaSdr:
+ DeviceProfileStore   — per-device calibraties, frequentie-offsets, gain cal.
+ Rx2DeviceStore       — koppeling van aux devices aan sessies
+ StationProfileStore  — multi-device sessie snapshots
+ MultiDeviceLayoutStore — UI layout per device-combinatie
```

### StationProfile model

```csharp
public sealed record StationProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DeviceProfileRef PrimaryDevice { get; init; }
    public IReadOnlyList<AuxiliaryDeviceProfileRef> AuxDevices { get; init; } = [];
    public IReadOnlyDictionary<HamBand, BandStackEntry> BandStack { get; init; } = new Dictionary<HamBand, BandStackEntry>();
    public AudioRoutingProfile AudioRouting { get; init; } = AudioRoutingProfile.Default;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastUsed { get; set; }
}
```

---

## 10.11 Laag 9: Telemetry / Logging / Diagnostics

### Implementatie

```csharp
// Program.cs / ZeusHost.cs
services.AddSerilog(config => config
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
    .WriteTo.File("logs/novasdr-.log", rollingInterval: RollingInterval.Day)
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "NovaSdr"));

services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("NovaSdr.Dsp")
        .AddSource("NovaSdr.Protocol")
        .AddSource("NovaSdr.Audio"));
```

### Diagnostics endpoints

```
GET /api/diagnostics/latency    → per-device end-to-end latency meting
GET /api/diagnostics/dsp        → DSP thread utilization, GC pauses
GET /api/diagnostics/wdsp       → WDSP channel status
GET /api/diagnostics/devices    → alle device statussen
GET /api/diagnostics/audio      → audio buffer stats (underruns, drops)
```

---

## 10.12 Dataflow: Volledig RX pad

```
[BRICK2 HARDWARE via LAN]
UDP 1035 (P2 DDC0 IQ stream)
       ↓
[Protocol2Client.cs — Network Thread]
UDP packets → IqFrame deserialisatie → BlockingCollection<IqBlock>
       ↓
[DspWorkerThread — Priority=Highest]
WdspDspEngine.FeedIq(ch=0, iqBlock)  → fexchange0() → RXA chain
  • Bandpass filter (nbp0)
  • CTUN shift (SetCtunShift)
  • Demodulator (LSB/USB/FM/AM/CW)
  • AGC (wcpAGC)
  • Noise Reduction (EMNR / NR1 / NR2)
  • Noise Blanker (SNB)
  • EQ (10-band)
       ↓
[PipelineTick — 30 Hz timer]
WdspDspEngine.ReadAudio(ch=0, audioBuffer)  → float32[] @ 48 kHz
WdspDspEngine.TryGetDisplayPixels(ch=0, Panadapter, fftBuffer)
       ↓
[IAudioRouter → SpscRing<float>]
       ↓
[miniaudio callback — Priority=Highest]
SpscRing.Read() → audio device output (speakers/headphones)
       ↓
[StreamingHub — SignalR]
Binary frame 0x12 (AUDIO_PCM) → browser Web Audio API (remote only)
Binary frame 0x11 (DISPLAY_FRAME) → WebGL canvas update @ 30 fps
Binary frame 0x19 (RX_METERS_V2) → React S-meter @ 5 Hz
```

## 10.13 Dataflow: Volledig TX pad (Protocol 2)

```
[BROWSER MIC via WebSocket]
Binary frame 0x20 (MIC_PCM) @ 48 kHz
       ↓
[TxAudioIngest.cs]
PCM frames → TXA input ring buffer
       ↓
[DSP Worker Thread — Priority=Highest]
WdspDspEngine WDSP TXA chain:
  • EQ (mic pre-emphasis)
  • Leveler (makeup gain)
  • CFC (10-band voice shaper)
  • Compressor (optional)
  • ALC
  • Limiter
  in=512@48k → dsp=1024@96k → out=2048@192k (CFIR upsample)
       ↓
[Protocol2Client.cs — Network Thread]
IQ @ 192 kHz → UDP DUC stream → Brick2
       ↓
[BRICK2 HARDWARE — RF OUTPUT]
```

---

## 10.14 Afwijkingen van Zeus v0.1

NovaSdr is geen directe fork van Zeus — het is een evolutie met de volgende aanvullingen:

| Component | Zeus v0.1 | NovaSdr |
|---|---|---|
| Hardware abstraction | Impliciet (P1/P2 alleen) | Expliciet `IDeviceSource`/`ITransceiver` HAL |
| Multi-device | Niet aanwezig | Volledig RadioSession model |
| RTL-SDR support | Niet aanwezig | `RtlSdrSource` via librtlsdr |
| SDRplay support | Niet aanwezig | `SdrplaySource` (native API + SoapySDR) |
| PlutoSDR support | Niet aanwezig | `PlutoSdrSource`/`PlutoSdrTransceiver` |
| DeviceCoordinator | Niet aanwezig | Volledig PTT lockout + freq sync |
| CAT plugin | Beperkt | Kenwood-compat CAT plugin |
| N1MM plugin | Niet aanwezig | UDP streaming plugin |
| DX Cluster | Niet aanwezig | Telnet DX cluster plugin |
| Station profiles | Niet aanwezig | Multi-device sessie snapshots |
| Solar/greyline | Niet aanwezig | Solar data plugin |
