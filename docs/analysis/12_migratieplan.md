# NovaSdr — Fase 12: Migratie- en Bouwplan
*Gefaseerd stappenplan | Gegenereerd: 2026-05-29*

---

## Strategie: Evolutie, niet Greenfield

De slimste route is **evolutie van OpenHPSDR-Zeus** met selectieve overname van features en code-patronen uit deskHPSDR en Thetis. Geen volledige rewrite.

```
Strategie keuze:
✗ Greenfield   — te langzaam, alles herschrijven
✓ Evolutie     — Zeus als basis, uitbreiden met HAL + multi-device + feature-sets
✗ Hybrid reuse — code copy-paste uit drie codebases = onderhouds-nachtmerrie
```

---

## 12.1 Herbruikbaarheidsmatrix

### Direct hergebruiken (Zeus — ongewijzigd)

| Component | Bestand(en) | Actie |
|---|---|---|
| Protocol 1 client | `Zeus.Protocol1/Protocol1Client.cs`, `ControlFrame.cs`, `PacketParser.cs` | Volledig hergebruiken |
| Protocol 2 client | `Zeus.Protocol2/Protocol2Client.cs`, `IqFrame.cs`, `P2TelemetryReading.cs` | Volledig hergebruiken |
| Protocol 1 discovery | `Zeus.Protocol1/Discovery/RadioDiscoveryService.cs`, `ReplyParser.cs` | Volledig hergebruiken |
| Protocol 2 discovery | `Zeus.Protocol2/Discovery/RadioDiscoveryService.cs` | Volledig hergebruiken |
| DSP interface + impl | `Zeus.Dsp/IDspEngine.cs`, `WdspDspEngine.cs`, `NativeMethods.cs` | Volledig hergebruiken |
| DSP contracts | `Zeus.Dsp/RxStageMeters.cs`, `TxStageMeters.cs`, `PsStageMeters.cs` | Volledig hergebruiken |
| Wire formats | `Zeus.Contracts/MsgType.cs`, `*Frame.cs`, `Dtos.cs` | Volledig hergebruiken |
| Plugin SDK | `Zeus.Plugins.Contracts/IZeusPlugin.cs`, `IPluginContext.cs`, `PluginManifest.cs` | Volledig hergebruiken |
| Plugin loader | `Zeus.Plugins.Host/` | Volledig hergebruiken |
| TCI server | `Zeus.Server.Hosting/Tci/TciProtocol.cs`, `TciServer.cs`, `TciSession.cs` | Volledig hergebruiken |
| Audio (miniaudio) | `Zeus.Server.Hosting/MiniAudioInput.cs`, `MiniAudioOutput.cs` | Volledig hergebruiken |
| CW engine | `Zeus.Server.Hosting/CwEngine.cs` | Volledig hergebruiken |
| Application services | `DspPipelineService.cs`, `TxService.cs`, `RadioCalibrations.cs` | Hergebruiken, uitbreiden |
| SignalR hub | `Zeus.Server.Hosting/StreamingHub.cs` | Hergebruiken, uitbreiden voor RX2 frames |
| REST API | `Zeus.Server.Hosting/ZeusEndpoints.cs` | Hergebruiken, uitbreiden |
| Persistence stores | `Zeus.Server.Hosting/*Store.cs` (50+ stores) | Volledig hergebruiken |
| WebGL renderer | `zeus-web/src/gl/` | Volledig hergebruiken |
| Zustand stores | `zeus-web/src/state/` (25+ stores) | Hergebruiken, uitbreiden |
| React componenten | `zeus-web/src/components/` | Hergebruiken, uitbreiden |
| Dockable workspace | `zeus-web/src/layout/FlexWorkspace.tsx` | Volledig hergebruiken |
| REST API client | `zeus-web/src/api/` | Hergebruiken, uitbreiden |
| Native libraries | `Zeus.Dsp/runtimes/*/native/` | Volledig hergebruiken |

### Uitbreiden (Zeus — aanpassen)

| Component | Bestaand | Uitbreiding |
|---|---|---|
| `StreamingHub.cs` | Primary display/audio/meters | + RX2 frame types (0x21-0x2F range) |
| `ZeusEndpoints.cs` | Primary radio endpoints | + `/api/devices`, `/api/rx2/*`, `/api/session/*` |
| `DspPipelineService.cs` | Primary DSP pipeline | + kanaal-ID allocatie + aux device registratie |
| `PluginCapabilities.cs` | 7 capabilities | + ReceiveRx2Stream, ControlRx2, AccessHardware, StreamN1mmUdp, DxClusterConnect |
| `IPluginContext.cs` | Primary device access | + `Rx2Device`, `Rx2Radio` properties |
| `zeus-web/src/state/` | Primary device stores | + `deviceRegistry-store.ts`, `rx2-store.ts`, `session-store.ts` |

### Nieuw bouwen

| Component | Gebaseerd op | Prioriteit |
|---|---|---|
| `IDeviceSource` interface | SDR++ module.h (concept) | MVP |
| `ITransceiver` interface | IDeviceSource uitbreiding | MVP |
| `DeviceCapabilities` flags enum | Analyse hoofdstuk 7 | MVP |
| `DeviceRegistry` + `WdspChannelAllocator` | Nieuw | MVP |
| `DeviceCoordinatorService` | Nieuw | MVP |
| `Rx2PipelineService` | DspPipelineService patroon | MVP |
| `SampleRateBridge` | libsamplerate | MVP |
| `RtlSdrSource` | librtlsdr P/Invoke | MVP |
| `SoapySdrSource` | SoapySDR P/Invoke | Fase 1 |
| `MultiDevicePanel.tsx` | React + Zeus componenten | MVP |
| `DeviceManagerPanel.tsx` | React + Zeus componenten | MVP |
| `AuxiliaryReceiverPanel.tsx` | React + PrimaryReceiverPanel | MVP |
| `SdrplaySource` | SDRplay API 3.x P/Invoke | Fase 2 |
| `PlutoSdrSource` / `PlutoSdrTransceiver` | libiio P/Invoke | Fase 2 |
| `SaturnExtendedTransceiver` | deskHPSDR saturnmain.c (referentie) | Fase 2 |
| CAT plugin | Thetis CATCommands.cs (referentie) | Fase 2 |
| N1MM plugin | Thetis N1MM.cs (referentie) | Fase 2 |
| DX Cluster plugin | deskHPSDR dxcluster.c (model) | Fase 3 |
| Solar/greyline plugin | deskHPSDR libsolar/ (model) | Fase 3 |
| `StationProfileStore` | LiteDB model | Fase 2 |
| `FreqSyncPolicy` + implementatie | Nieuw | MVP |
| `IAudioRouter` | Nieuw | MVP |

### Referentie (code niet overnemen, logica begrijpen)

| Broncode | Project | Gebruik als referentie voor |
|---|---|---|
| `old_protocol.c` | deskHPSDR | P1 edge cases, timing, HL2 quirks validatie |
| `new_protocol.c` | deskHPSDR | P2 edge cases, Saturn register validatie |
| `saturnmain.c` / `saturnregisters.h` | deskHPSDR | Saturn-specific P2 register uitbreidingen |
| `rigctl.c` | deskHPSDR | CAT command structuur referentie |
| `CATCommands.cs` | Thetis | Kenwood TS-2000 command set (7000+ lijnen), 1-op-1 command mapping |
| `NetworkIO.cs` | Thetis | P1/P2 auto-detect flow |
| `N1MM.cs` | Thetis | N1MM UDP packet format en timing |
| `TCIServer.cs` | Thetis | TCI feature vergelijking met Zeus implementatie |
| `clsRadioDiscovery.cs` | Thetis | Discovery edge cases (meerdere NIC's) |
| `audio.cs` | Thetis | ASIO routing concepten voor ASIO plugin |

---

## 12.2 MVP Scope (0-3 maanden)

**Doel:** Werkende NovaSdr applicatie voor Brick2 (P1+P2) + RTL-SDR als RX2 monitor

### Sprint 1 (weken 1-2): Repository setup + interfaces

```
□ Fork Zeus → NovaSdr repository (GitHub)
□ Rename OpenhpsdrZeus → NovaSdr in solution
□ Definieer IDeviceSource interface (Zeus.Devices/IDeviceSource.cs)
□ Definieer ITransceiver interface (Zeus.Devices/ITransceiver.cs)
□ Definieer DeviceCapabilities [Flags] enum
□ OpenHpsdrP1Transceiver : ITransceiver (wraps Protocol1Client)
□ OpenHpsdrP2Transceiver : ITransceiver (wraps Protocol2Client)
□ Alle bestaande Zeus tests groen houden
□ CI/CD pipeline opzetten (GitHub Actions)
```

### Sprint 2 (weken 3-4): WdspChannelAllocator + DeviceRegistry

```
□ WdspChannelAllocator (partitionering 0-13 primary, 16-29 aux)
□ DeviceRegistry implementatie
□ DiscoveryAggregatorService (start met OpenHPSDR discovery enkel)
□ RadioSession domain model
□ AuxiliaryReceiver model
□ Unit tests voor channel allocatie en registry
```

### Sprint 3 (weken 5-6): RTL-SDR device + SampleRateBridge

```
□ librtlsdr P/Invoke signatures (NativeRtlsdrMethods.cs)
□ RtlSdrSource : IDeviceSource implementatie
□ RtlSdrEnumerator (discover aangesloten dongels)
□ SampleRateBridge (2.048 MHz → 48 kHz via polyphase FIR)
□ Integratie: RTL-SDR IQ → SampleRateBridge → WdspDspEngine.FeedIq(ch=16)
□ Test: RTL-SDR spectrum zichtbaar in debug log
```

### Sprint 4 (weken 7-8): Rx2PipelineService + DeviceCoordinator

```
□ Rx2PipelineService : BackgroundService
  - Eigen DSP channel management (ch 16-29)
  - Eigen display pixel generatie
  - Eigen audio ring → IAudioRouter
□ DeviceCoordinatorService : BackgroundService
  - PTT lockout (primary MOX → aux SetMoxAsync(false))
  - FreqSyncPolicy.FollowPrimary implementatie
□ IAudioRouter implementatie (stereo routing: primary L, RX2 R)
□ StreamingHub uitbreiden: RX2 display frames (type 0x21)
□ REST API uitbreiden: /api/devices, /api/rx2/state, /api/session/attach-rx2
```

### Sprint 5 (weken 9-10): React multi-device UI

```
□ deviceRegistry-store.ts (Zustand)
□ rx2-store.ts (VFO, mode, gain, freqSync voor RX2)
□ AuxiliaryReceiverPanel.tsx (basic: freq, gain, mode display)
□ DeviceManagerPanel.tsx (discovery lijst, attach als aux knop)
□ MultiDevicePanel.tsx (primary + RX2 side-by-side)
□ WebSocket handler uitbreiden voor 0x21 RX2 display frames
□ RX2 WebGL canvas component
```

### Sprint 6 (weken 11-12): MVP validatie + bugfixes

```
□ End-to-end test: Brick2 P2 + RTL-SDR RX2 simultaan
□ PTT lockout test
□ Frequentiesync test (FollowPrimary)
□ Audio routing test (stereo: primary L, RX2 R)
□ Performance profiling: CPU load bij dual device
□ Documentatie update: README, CONTRIBUTING
□ MVP release tag (v0.2.0-mvp)
```

**MVP Deliverable:** Werkende NovaSdr met Brick2 als primaire transceiver + RTL-SDR als RX2 spectrum monitor, side-by-side WebGL displays, PTT lockout, frequentiesync.

---

## 12.3 Fase 2 Scope (3-6 maanden)

**Doel:** SDRplay + PlutoSDR support + CAT + N1MM

### Prioriteiten Fase 2

```
□ SoapySdrSource : IDeviceSource (SoapySDR P/Invoke)
  - RTL-SDR via SoapySDR fallback pad
  - SDRplay via SoapySDRPlay3 driver
□ SdrplaySource : IDeviceSource (native API 3.x)
  - Hardware AGC, notch filters, hardware attenuator via P/Invoke
  - Als optionele plugin (proprietaire API)
□ PlutoSdrSource : IDeviceSource (libiio P/Invoke)
  - Ethernet verbinding (192.168.2.1 default)
  - Sample rate 2.5 MHz → SampleRateBridge → 48 kHz
□ PlutoSdrTransceiver : ITransceiver
  - Full-duplex TX + RX
  - PlutoPlus uitgebreid bereik (70 MHz – 6 GHz)
□ FreqSyncPolicy.FollowPrimaryWithOffset
□ IAudioRouter stereo mix verbeteringen
□ CAT plugin (IBackendPlugin)
  - Kenwood TS-2000 emulatie (gebaseerd op Thetis CATCommands.cs)
  - TCP server op poort 4532 (Hamlib compatible)
□ N1MM plugin (IBackendPlugin)
  - UDP spectrum streaming (gebaseerd op Thetis N1MM.cs)
  - Poort 13065 (N1MM standaard)
□ StationProfileStore + UI
  - Multi-device sessies opslaan/laden
□ Saturn-specific P2 uitbreidingen
  - XDMA register support (referentie: deskHPSDR saturnmain.c)
□ ASIO plugin (Windows, optioneel)
  - cmASIO model (Thetis referentie)
  - Lagere audio latency voor Windows power users
```

**Fase 2 Deliverable:** SDRplay of RTL-SDR als volwaardige RX2, CAT voor WSJT-X/logging software koppeling, N1MM voor contest logging.

---

## 12.4 Fase 3 Scope (6-12 maanden)

**Doel:** Feature pariteit met deskHPSDR/Thetis + mobile/tablet UX

```
□ DX Cluster plugin (telnet, spot overlay op panadapter)
  - Referentie: deskHPSDR dxcluster.c
□ Solar/greyline plugin
  - Referentie: deskHPSDR libsolar/
□ PSK Reporter plugin
□ WSPR/FT8 monitor plugin (RX2 als digitale mode receiver)
  - VAC/IPC brug naar WSJT-X
□ Discord bot plugin (referentie: Thetis clsDiscord.cs)
□ FreeDV integratie (mode DIGU/DIGL + codec2)
□ Capacitor iOS/Android tablet UI optimalisaties
  - Touch-geoptimaliseerde VFO (swipe voor tunen)
  - Adaptive layout voor landscape/portrait
  - Grote touch targets voor alle controls
□ Multi-preset UI (snelle configuratie-switch)
□ PureSignal 2.0 via PlutoPlus feedback (experimenteel)
□ Satellite rotator plugin (API koppeling)
□ ADIF logging (QSO logboek, import/export)
□ Band planning geavanceerd (lokale bandplannen, IARU Region 1/2/3)
□ Performance hardening
  - Native AOT (.NET AOT compilation voor snellere startup)
  - WDSP SIMD optimalisaties (AVX2 flags)
  - WebGL waterfall optimalisaties (shaders)
□ Documentatie: volledig gebruikersgids
```

---

## 12.5 Gefaseerde Roadmap

```
FASE 0: Analyse (voltooid)
│   Codebase analyse deskHPSDR/Zeus/Thetis
│   SDR++/SDRangel architectuurreferentie
│   Doelarchitectuur ontwerp
│   ✓ Dit document
│
├─── FASE MVP (0-3 maanden)
│    ├─ Sprint 1-2: Repository setup + interfaces
│    ├─ Sprint 3: RTL-SDR device adapter
│    ├─ Sprint 4: Rx2PipelineService + DeviceCoordinator
│    ├─ Sprint 5: Multi-device React UI
│    └─ Sprint 6: MVP validatie
│    🎯 Brick2 P1/P2 + RTL-SDR als RX2
│
├─── FASE 2 (3-6 maanden)
│    ├─ SoapySdrSource + SdrplaySource
│    ├─ PlutoSdrSource + PlutoSdrTransceiver
│    ├─ CAT plugin (Kenwood TS-2000 compat)
│    ├─ N1MM plugin
│    ├─ StationProfile UI
│    └─ Saturn P2 uitbreidingen
│    🎯 SDRplay/Pluto als RX2, CAT/N1MM integratie
│
└─── FASE 3 (6-12 maanden)
     ├─ DX Cluster + Solar + PSK Reporter plugins
     ├─ WSPR/FT8 monitor plugin
     ├─ Capacitor mobile optimalisaties
     ├─ ADIF logging
     └─ Performance hardening
     🎯 Feature pariteit + mobile tablet UX
```

---

## 12.6 Concrete Eerste Stappen (Vandaag)

1. **Fork Zeus repo:** `gh repo fork Kb2uka/openhpsdr-zeus --rename NovaSdr`
2. **Maak tracking issue:** "Implement IDeviceSource / ITransceiver HAL layer"
3. **Bestel RTL-SDR V3/V4** als primaire RX2 testdongle (€25-35)
4. **Verbind Brick2 via P2** en verifieer Zeus v0.1 werkt als startpunt
5. **Lees** `Zeus.Protocol2/Protocol2Client.cs` en `Zeus.Dsp/IDspEngine.cs` — dit zijn de architecturele kern
6. **Schrijf eerste test:** `DeviceRegistryTests.cs` voor IDeviceSource registration

---

## 12.7 Risico's in Migratiepad

| Risico | Impact | Mitigatie |
|---|---|---|
| Zeus v0.1 heeft onontdekte bugs | Hoog | Valideer alle Zeus tests voor fork; gebruik hardware (Brick2 + HL2) voor smoke tests |
| librtlsdr API instabiliteit op Windows | Midden | Pin librtlsdr versie; test op alle drie platforms in CI |
| WDSP channel ID conflicts | Hoog | WdspChannelAllocator unit tests als vroege CI check |
| Capacitor audio latency op iOS | Hoog | Mobile scope bewust beperkt tot monitoring; TX uitgesloten van mobile |
| SDRplay API licentie compliance | Hoog | Lees SDRplay SDK agreement; distribueer als separate binary plugin |
| React bundle size groeit | Laag | Vite tree-shaking + lazy panel loading |
