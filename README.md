# NovaSdr

> **Next-generation crossplatform SDR RX/TX application for amateur radio operators**

[![License: GPL v2+](https://img.shields.io/badge/License-GPL%20v2%2B-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-green.svg)]()
[![Status](https://img.shields.io/badge/Status-Architecture%20%2F%20Planning-yellow.svg)]()

---

## Project Doel

NovaSdr is een moderne, stabiele, snelle en goed onderhoudbare crossplatform SDR-applicatie voor desktop, tablet en mobiel. Het combineert het beste van drie bestaande SDR-codebases in één nieuwe, toekomstbestendige architectuur.

**Primaire doelen:**
- Brick2 SDR hardware support via Protocol 2 én Protocol 1
- Modern, gebruiksvriendelijk interface (HamDash-geïnspireerde UX)
- Multi-device RX2: tweede SDR-device als auxiliary receiver
- Plugin-ecosysteem voor digitale modes, DX cluster, logging, CAT
- Crossplatform: Windows, Linux, macOS — tablet en mobiel als uitbreiding

---

## Architectuur Basis

NovaSdr is een **evolutie van OpenHPSDR-Zeus** met:

| Laag | Technologie |
|---|---|
| Backend | C# .NET 10 + ASP.NET Core |
| DSP Engine | WDSP (Warren Pratt NR0V) via IDspEngine P/Invoke |
| Audio | miniaudio (cross-platform native) |
| Frontend | React 19 + Vite + TailwindCSS 4 + WebGL |
| Desktop wrapper | Photino.NET |
| Mobile | Capacitor 6.2 (iOS/Android) |
| Database | LiteDB (embedded) |
| Protocols | OpenHPSDR P1 + P2 + SoapySDR + libiio |

---

## Hardware Ondersteuning

| Hardware | Protocol | Capabilities | Status |
|---|---|---|---|
| **Brick2 SDR** | HPSDR P1 + P2 | TX+RX+PureSignal+DualRx | **Primair** |
| Hermes-Lite 2 | HPSDR P1/P2 | TX+RX | Ondersteund |
| ANAN G2/G2 MkII | HPSDR P2 | TX+RX+PureSignal | Ondersteund |
| Saturn G2 | HPSDR P2 native | TX+RX+Diversity | Fase 2 |
| **SDRplay RSP serie** | Native API 3.x | RX+HwAGC+HardAtt | Fase 2 (user-installed API) |
| **RTL-SDR V3/V4** | librtlsdr | RX+DirectSample | **MVP RX2** |
| **PlutoSDR / PlutoPlus** | libiio | TX+RX+FullDuplex | Fase 2 |

> **SDRplay:** SDRplay API 3.15 is gratis beschikbaar via [sdrplay.com/api](https://www.sdrplay.com/api/) voor Windows, Linux en macOS. NovaSdr detecteert de geïnstalleerde API automatisch.

---

## Multi-Device RX2

NovaSdr ondersteunt een tweede SDR-device als auxiliary receiver naast de primaire transceiver:

```
RadioSession
├── PrimaryTransceiver (Brick2 P2) — TX + RX
└── AuxiliaryRx[0] (SDRplay / RTL-SDR / PlutoSDR) — RX monitor
```

**Features:**
- Onafhankelijke VFO per device
- Frequentiesync: RX2 volgt primary VFO (optioneel)
- PTT lockout: RX2 TX automatisch geblokkeerd bij primary TX
- Audio routing: stereo (primary L, RX2 R), mono mix, of mute

---

## Roadmap

### MVP (0-3 maanden)
- [x] Architectuuranalyse en ontwerp
- [ ] IDeviceSource / ITransceiver HAL
- [ ] RTL-SDR als RX2 device
- [ ] Rx2PipelineService + DeviceCoordinator
- [ ] Multi-device React panels
- [ ] Brick2 P1+P2 + RTL-SDR end-to-end test

### Fase 2 (3-6 maanden)
- [ ] SDRplay native API adapter
- [ ] PlutoSDR / PlutoPlus adapter (libiio)
- [ ] CAT plugin (Kenwood TS-2000 compatibel)
- [ ] N1MM Logger+ UDP streaming plugin
- [ ] Station profiles (multi-device sessies)

### Fase 3 (6-12 maanden)
- [ ] DX Cluster plugin
- [ ] Solar / greyline propagatie plugin
- [ ] FT8/WSPR monitor plugin (RX2 als digitale mode receiver)
- [ ] Capacitor iOS/Android tablet UI optimalisaties
- [ ] ADIF QSO logging

---

## Analyse Documentatie

In de `docs/analysis/` map vind je het volledige masterrapport:

| Document | Inhoud |
|---|---|
| [00 Executive Summary](docs/analysis/00_executive_summary.md) | Managementsamenvatting |
| [01a deskHPSDR](docs/analysis/01_inventarisatie_deskhpsdr.md) | Broncode-analyse deskHPSDR |
| [01b Zeus](docs/analysis/01_inventarisatie_zeus.md) | Broncode-analyse OpenHPSDR-Zeus |
| [01c Thetis](docs/analysis/01_inventarisatie_thetis.md) | Broncode-analyse Thetis |
| [02a Architectuur](docs/analysis/02_architectuur_per_project.md) | Architectuurmapping per project |
| [02b SDR++ / SDRangel](docs/analysis/02_referentie_sdrpp_sdrangel.md) | Referentieprojecten analyse |
| [03 Protocol/Hardware](docs/analysis/03_protocol_hardware_analyse.md) | P1+P2 protocol, Brick2, HAL |
| [04 DSP/Audio](docs/analysis/04_dsp_audio_analyse.md) | WDSP, DSP-ketens, audio stacks |
| [05 UI/UX](docs/analysis/05_ui_ux_analyse.md) | UI analyse + NovaSdr UX-principes |
| [06 Plugins/Integraties](docs/analysis/06_integratie_plugins.md) | Plugin architectuur, CAT, TCI, N1MM |
| [07 Extra Hardware](docs/analysis/07_extra_hardware_compatibility.md) | SDRplay, RTL-SDR, PlutoSDR |
| [08 Multi-Device RX2](docs/analysis/08_multi_device_rx2.md) | Multi-device architectuur |
| [09 Vergelijkingsmatrix](docs/analysis/09_vergelijkingsmatrix.md) | 22-criteria scores per project |
| [10 Doelarchitectuur](docs/analysis/10_doel_architectuur.md) | 9-laags NovaSdr architectuur |
| [11 Tech Stack](docs/analysis/11_tech_stack.md) | Stack keuze onderbouwing |
| [12 Migratieplan](docs/analysis/12_migratieplan.md) | MVP + fase 2 + fase 3 roadmap |
| [13 Risicoanalyse](docs/analysis/13_risicoanalyse.md) | 18 risico's met mitigaties |
| [14 Aanbevelingen](docs/analysis/14_aanbevelingen_open_vragen.md) | Concrete acties + open vragen |

---

## Gebaseerd op / Geïnspireerd door

| Project | Licentie | Gebruik in NovaSdr |
|---|---|---|
| [OpenHPSDR-Zeus](https://github.com/Kb2uka/openhpsdr-zeus) | GPL v2+ | **Architectuurbasis** |
| [deskHPSDR](https://github.com/dl1bz/deskhpsdr) | GPL v3 | Protocol referentie |
| [Thetis](https://github.com/ramdor/Thetis) | GPL v2 | Feature referentie |
| [WDSP](https://github.com/TAPR/OpenHPSDR-wdsp) | GPL v2+ | DSP engine (Warren Pratt NR0V) |
| [SDR++](https://github.com/AlexandreRouma/SDRPlusPlus) | GPL v3 | Plugin architectuur inspiratie |
| [SDRangel](https://github.com/f4exb/sdrangel) | GPL v3 | Channel plugin concept |

---

## Licentie

GNU General Public License v2 or later (GPL-2.0-or-later)

Zie [LICENSE](LICENSE) voor de volledige licentietekst.

---

## Contact / Bijdragen

- **Callsign:** PE5RBM (Ronald)
- **GitHub:** [@ronald1711](https://github.com/ronald1711)

Bijdragen zijn welkom! Zie [CONTRIBUTING.md](CONTRIBUTING.md) voor richtlijnen.
