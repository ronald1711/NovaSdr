# NovaSdr — Analyse Documentatie

Volledige masteranalyse van drie SDR-codebases (deskHPSDR, OpenHPSDR-Zeus, Thetis)
plus architectuurontwerp voor de NovaSdr applicatie.

**Totaal:** 18 bestanden · ~465 KB · ~11.000 regels · Gegenereerd: 2026-05-29

## Inhoudsopgave

| # | Document | Inhoud |
|---|---|---|
| 00 | [Executive Summary](00_executive_summary.md) | Managementsamenvatting, top-5 aanbevelingen en risico's |
| 01a | [Inventarisatie deskHPSDR](01_inventarisatie_deskhpsdr.md) | C/GTK3, Protocol 1+2, WDSP, afhankelijkheden |
| 01b | [Inventarisatie Zeus](01_inventarisatie_zeus.md) | .NET 10 + React 19, IDspEngine, plugin SDK |
| 01c | [Inventarisatie Thetis](01_inventarisatie_thetis.md) | WinForms, gearchiveerd, feature-inventaris |
| 02a | [Architectuur per project](02_architectuur_per_project.md) | Dataflow, thread-model, bottlenecks |
| 02b | [SDR++ en SDRangel referentie](02_referentie_sdrpp_sdrangel.md) | Plugin architectuur vergelijking |
| 03 | [Protocol en hardware](03_protocol_hardware_analyse.md) | P1/P2 frames, Brick2, HAL interface-ontwerp |
| 04 | [DSP en audio](04_dsp_audio_analyse.md) | WDSP, RXA/TXA ketens, audio stacks |
| 05 | [UI/UX analyse](05_ui_ux_analyse.md) | Per-project UI + NovaSdr UX-principes |
| 06 | [Integraties en plugins](06_integratie_plugins.md) | CAT, TCI, N1MM, DX cluster, plugin SDK |
| 07 | [Extra hardware](07_extra_hardware_compatibility.md) | SDRplay, RTL-SDR, PlutoSDR/PlutoPlus |
| 08 | [Multi-device RX2](08_multi_device_rx2.md) | Tweede device als auxiliary receiver |
| 09 | [Vergelijkingsmatrix](09_vergelijkingsmatrix.md) | 22-criteria scores 1-10 per project |
| 10 | [Doelarchitectuur](10_doel_architectuur.md) | 9-laags NovaSdr architectuur + ASCII-diagrammen |
| 11 | [Tech stack](11_tech_stack.md) | Stackkeuze + verworpen alternatieven |
| 12 | [Migratieplan](12_migratieplan.md) | MVP → fase 2 → fase 3 stappenplan |
| 13 | [Risicoanalyse](13_risicoanalyse.md) | 18 risico's met ernst/kans/mitigatie |
| 14 | [Aanbevelingen](14_aanbevelingen_open_vragen.md) | 10 concrete acties + 8 open vragen |

## Kernbevinding

> **NovaSdr = Zeus (architectuurbasis) + deskHPSDR (protocol referentie) + Thetis (feature referentie)**
>
> Aanbevolen stack: **C# .NET 10** + **React 19 + WebGL** + **WDSP** + **miniaudio**
>
> Strategie: **Evolutie van OpenHPSDR-Zeus** — geen greenfield, geen code copy-paste
