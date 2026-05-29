# PantheonSDR

> **Next-generation crossplatform SDR transceiver application for amateur radio operators**

[![License: GPL v2+](https://img.shields.io/badge/License-GPL%20v2%2B-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-green.svg)]()
[![Status](https://img.shields.io/badge/Status-Early%20Development-orange.svg)]()
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)]()
[![React](https://img.shields.io/badge/React-19-61DAFB.svg)]()

---

## What is PantheonSDR?

PantheonSDR is a modern, stable, and cross-platform SDR RX/TX application for desktop, tablet, and mobile. It combines the best architectural patterns from three existing SDR codebases into one clean, maintainable, and extensible foundation.

**Core goals:**
- Full support for Brick2 SDR hardware via OpenHPSDR Protocol 1 and Protocol 2
- Modern, operator-friendly UI inspired by HamDash and SDR Console
- **Multi-device:** combine any SDR hardware as primary transceiver + auxiliary receivers
- Plugin ecosystem for digital modes, DX cluster, logging, and CAT control
- Cross-platform: Windows, Linux, macOS — tablet and mobile as extension

---

## Architecture

PantheonSDR evolves **OpenHPSDR-Zeus** with a full hardware abstraction layer (HAL):

```
┌─────────────────────────────────────────────────────────┐
│  Presentation    React 19 + WebGL + TailwindCSS 4        │
│  Desktop: Photino.NET  │  Mobile: Capacitor 6            │
├─────────────────────────────────────────────────────────┤
│  Application     ASP.NET Core .NET 10 + SignalR          │
│  DeviceCoordinator │ Rx2Pipeline │ Discovery              │
├─────────────────────────────────────────────────────────┤
│  SDR Domain      RadioSession │ AttachedDevice            │
│  FreqSyncPolicy │ AudioRoute │ PTT Lockout                │
├─────────────────────────────────────────────────────────┤
│  DSP / Audio     WDSP (Warren Pratt) via IDspEngine       │
│  miniaudio │ SampleRateBridge (decimation)               │
├─────────────────────────────────────────────────────────┤
│  Hardware HAL    IDeviceSource │ ITransceiver              │
│  DeviceRegistry │ WdspChannelAllocator                   │
├──────────┬──────────┬─────────────┬──────────────────────┤
│ HPSDR P1 │ HPSDR P2 │ SDRplay API │ libiio (PlutoSDR)    │
└──────────┴──────────┴─────────────┴──────────────────────┘
```

| Layer | Technology |
|---|---|
| Backend | C# .NET 10 + ASP.NET Core |
| DSP Engine | WDSP (Warren Pratt NR0V) via `IDspEngine` P/Invoke |
| Audio | miniaudio (cross-platform native) |
| Frontend | React 19 + Vite + TailwindCSS 4 + WebGL |
| Desktop | Photino.NET |
| Mobile | Capacitor 6.2 (iOS/Android) |
| Database | LiteDB (embedded) |
| Protocols | OpenHPSDR P1/P2 + SDRplay API 3.x + libiio |

---

## Multi-Device Support

PantheonSDR uses a **symmetric device model** — any device can be primary or auxiliary:

```
RadioSession
├── Primary (TX + RX):   Brick2 P2 / PlutoSDR Plus / Hermes-Lite 2 / ...
└── Auxiliary (RX):      SDRplay RSP1A / PlutoSDR / second OpenHPSDR / ...
```

**Supported combinations (examples):**

| Primary | Auxiliary | Notes |
|---|---|---|
| Brick2 P2 | SDRplay RSP1A | HF transceiver + broadband monitor |
| Brick2 P2 | PlutoSDR Plus F5OEO | HF TX/RX + VHF/UHF RX |
| Brick2 P2 | Hermes-Lite 2 | Two OpenHPSDR devices simultaneously |
| PlutoSDR Plus | SDRplay RSP1A | VHF/UHF primary + HF monitor |
| Brick2 P2 | SDRplay + PlutoSDR | Three devices simultaneously |

**Automatic PTT lockout:** when primary transmits, all auxiliary `ITransceiver` devices are automatically blocked from transmitting.

**Frequency synchronisation** per auxiliary device:
- `Independent` — own VFO (default)
- `FollowPrimary` — tracks primary VFO automatically
- `FollowPrimaryWithOffset` — with fixed offset (satellite, transverter)

---

## Hardware Support

| Hardware | Protocol | Capabilities | Status |
|---|---|---|---|
| **Brick2 SDR** | HPSDR P1 + P2 | TX+RX+PureSignal+DualRx | **Primary** |
| Hermes-Lite 2 | HPSDR P1/P2 | TX+RX | Supported |
| ANAN G2/G2 MkII | HPSDR P2 | TX+RX+PureSignal | Supported |
| ANAN series (100/200D/7000/8000) | HPSDR P1/P2 | TX+RX | Supported |
| Saturn G2 | HPSDR P2 native | TX+RX+Diversity | Phase 2 |
| **SDRplay RSP1A** | SDRplay API 3.x | RX+HwAGC+HardAtt | **Implemented** |
| SDRplay RSP2/RSPdx/RSPduo | SDRplay API 3.x | RX+HwAGC | Phase 2 |
| **PlutoSDR Plus (F5OEO fw)** | libiio | TX+RX+FullDuplex, 70 MHz–6 GHz | **Implemented** |
| PlutoSDR (standard) | libiio | TX+RX, 325 MHz–3.8 GHz | Supported |
| RTL-SDR V3/V4 | librtlsdr | RX+DirectSample | Phase 2 |

> **SDRplay:** SDRplay API 3.15 is freely available at [sdrplay.com/api](https://www.sdrplay.com/api/)  
> for Windows, Linux, and macOS. PantheonSDR auto-detects the installed API.

> **PlutoSDR Plus:** Uses the F5OEO custom firmware which unlocks the AD9364 chip  
> to cover 70 MHz–6 GHz with up to 56 MHz bandwidth.

---

## Source Projects & Inspiration

| Project | License | Role in PantheonSDR |
|---|---|---|
| [OpenHPSDR-Zeus](https://github.com/Kb2uka/openhpsdr-zeus) | GPL v2+ | **Architecture foundation** |
| [deskHPSDR](https://github.com/dl1bz/deskhpsdr) | GPL v3 | Protocol reference |
| [Thetis](https://github.com/ramdor/Thetis) | GPL v2 | Feature reference (CAT, N1MM, TCI) |
| [WDSP](https://github.com/TAPR/OpenHPSDR-wdsp) | GPL v2+ | DSP engine (Warren Pratt NR0V) |
| [SDR++](https://github.com/AlexandreRouma/SDRPlusPlus) | GPL v3 | Plugin architecture reference |
| [SDRangel](https://github.com/f4exb/sdrangel) | GPL v3 | Channel plugin model reference |

---

## Roadmap

### MVP (months 0–3)
- [x] Architecture analysis & design (18-document master report in `docs/analysis/`)
- [x] `IDeviceSource` / `ITransceiver` HAL
- [x] `WdspChannelAllocator` — WDSP channel partitioning
- [x] `DeviceRegistry` — hardware discovery registry
- [x] SDRplay RSP1A adapter (`SdrplaySource`, native API 3.x)
- [x] PlutoSDR Plus F5OEO adapter (`PlutoSdrTransceiver`, libiio)
- [x] `SampleRateBridge` — IQ decimation to 48 kHz for WDSP
- [x] `RadioSession` — symmetric multi-device session model
- [x] `DeviceCoordinatorService` — PTT lockout, frequency sync
- [x] `Rx2PipelineService` — dynamic per-device DSP pipelines
- [ ] Wire `RadioSession` into `Zeus.Server.Hosting` (REST API + SignalR)
- [ ] Multi-device React panels (primary + auxiliary spectra)
- [ ] End-to-end test: Brick2 P2 + SDRplay RSP1A

### Phase 2 (months 3–6)
- [ ] CAT plugin (Kenwood TS-2000 compatible, TCP 4532)
- [ ] N1MM Logger+ UDP streaming plugin
- [ ] RTL-SDR V3/V4 adapter
- [ ] Station profiles (save/load multi-device sessions)
- [ ] DX spot overlay on panadapter

### Phase 3 (months 6–12)
- [ ] DX Cluster plugin (telnet)
- [ ] Solar / greyline propagation plugin
- [ ] FT8/WSPR monitor plugin (auxiliary device as digital mode receiver)
- [ ] Capacitor iOS/Android tablet UI optimisations
- [ ] ADIF QSO logging

---

## Architecture Analysis

The `docs/analysis/` directory contains a comprehensive 18-document master analysis  
of three SDR codebases (deskHPSDR, OpenHPSDR-Zeus, Thetis) plus SDR++ and SDRangel  
as architectural references — totalling ~465 KB / ~11,000 lines.

| Document | Content |
|---|---|
| [00 Executive Summary](docs/analysis/00_executive_summary.md) | Key findings and recommendations |
| [01a deskHPSDR](docs/analysis/01_inventarisatie_deskhpsdr.md) | deskHPSDR source analysis |
| [01b Zeus](docs/analysis/01_inventarisatie_zeus.md) | OpenHPSDR-Zeus source analysis |
| [01c Thetis](docs/analysis/01_inventarisatie_thetis.md) | Thetis source analysis |
| [03 Protocol/Hardware](docs/analysis/03_protocol_hardware_analyse.md) | P1/P2 protocol, Brick2, HAL design |
| [07 Extra Hardware](docs/analysis/07_extra_hardware_compatibility.md) | SDRplay, RTL-SDR, PlutoSDR |
| [08 Multi-Device RX2](docs/analysis/08_multi_device_rx2.md) | Multi-device architecture |
| [10 Target Architecture](docs/analysis/10_doel_architectuur.md) | 9-layer architecture design |
| [12 Migration Plan](docs/analysis/12_migratieplan.md) | MVP → Phase 2 → Phase 3 |
| [13 Risk Analysis](docs/analysis/13_risicoanalyse.md) | 18 risks with mitigations |

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution guidelines.

Bug reports and feature requests: use [GitHub Issues](https://github.com/ronald1711/PantheonSDR/issues).

---

## License

GNU General Public License v2 or later (GPL-2.0-or-later)

See [LICENSE](LICENSE) for the full license text.

---

## Contact

- **Callsign:** PC3Y (Ronald)
- **GitHub:** [@ronald1711](https://github.com/ronald1711)
