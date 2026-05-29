# NovaSdr — Executive Summary

**Versie:** 1.0  
**Datum:** 2026-05-29  
**Auteur:** Architect review op basis van codebase-analyse en referentieonderzoek  
**Status:** Vastgesteld

---

## 1. Projectdoel

NovaSdr is een nieuw te ontwikkelen open-source SDR-applicatie gericht op de OpenHPSDR-hardwarefamilie (Hermes, Angelia, Orion, Anan-serie). Het doel is een moderne, crossplatform transceiver-frontend die de huidige Windows-only C#-applicaties (Thetis) en de C/GTK3-applicatie (deskHPSDR) vervangt of aanvult met een tijdige, onderhoudbare en uitbreidbare architectuur.

De applicatie moet:
- Protocol 1 en Protocol 2 (OpenHPSDR Ethernet) ondersteunen
- WDSP als DSP-bibliotheek integreren voor radiokwaliteit geluid en signaalverwerking
- Draaien op Windows, Linux en macOS (desktop-first, mobile later)
- Uitbreidbaar zijn via een plugin/module-systeem
- Een moderne UI bieden die niet afhankelijk is van verouderde platform-toolkits

---

## 2. Analyse scope

De analyse omvat drie primaire projecten:

| Code | Project | Taal | Platform |
|------|---------|------|----------|
| A | deskHPSDR | C + GTK3 | Linux/macOS |
| B | Zeus | C# .NET 10 + React 19/TS | Windows/Linux/macOS + mobile |
| C | Thetis | C# .NET 4.8 + WinForms | Windows ONLY (gearchiveerd) |

Aanvullend zijn twee externe SDR-projecten als architectuurreferentie geanalyseerd:
- **SDR++** (AlexandreRouma/SDRPlusPlus) — C++/ImGui, GPL-3.0, 5970 stars
- **SDRangel** (f4exb/sdrangel) — C++/Qt5/OpenGL, GPL-3.0, 3797 stars

---

## 3. Samenvatting drie primaire projecten

### PROJECT A — deskHPSDR

Pure C met GTK3 UI. Ondersteunt Protocol 1 en Protocol 2. Integreert WDSP als gedeelde bibliotheek (~200K regels). Codebase ca. 77K regels. Actief onderhouden op Linux en macOS. Sterke punten: lichte voetafdruk, directe hardware-integratie, productiekwaliteit. Zwakke punten: geen Windows, geen plugin-architectuur, GTK3 UI beperkt in modernisering, C-monoliet moeilijk te componentiseren.

### PROJECT B — Zeus

C# .NET 10 backend, React 19/TypeScript frontend via WebView/Capacitor, WebGL-gebaseerde spectrum/waterval UI. Versie 0.1 (april 2026). Ondersteunt Protocol 1 en Protocol 2 via aparte assemblies. IDspEngine interface maakt WDSP-koppeling mogelijk. Plugin-systeem aanwezig. Crossplatform by design. Sterke punten: moderne stack, mobiel-ready, Web-ecosysteem voor UI, .NET 10 performance, plugin-model aanwezig. Zwakke punten: vroeg stadium (v0.1), feature gap t.o.v. Thetis enorm, nog niet productierijp.

### PROJECT C — Thetis (GEARCHIVEERD april 2026)

C# .NET 4.8 met WinForms UI. Windows-only. Rijkste featureset van alle drie: CAT-protocol (~7K regels), N1MM Logger-integratie, Discord-integratie, TCI-protocol, uitgebreide panadapter. 53K-regel `console.cs` monoliet. Gearchiveerd april 2026 — geen verdere ontwikkeling. Waarde: een volledige referentie-implementatie van alle gewenste features, maar niet als basis bruikbaar vanwege Windows-lock-in en technische schuld.

---

## 4. Kernbevinding

**Zeus (Project B) is de enig logische basis voor NovaSdr.**

Thetis is gearchiveerd en architectureel dood. deskHPSDR mist een plugin-model en een moderne UI-stack. Zeus heeft de enige architectuur die:

1. Crossplatform is by design (.NET 10 + web frontend)
2. Een interface voor DSP-abstractie (IDspEngine) al definieert
3. Een plugin-systeem al begint op te bouwen
4. Protocol 1 en 2 al ondersteunt
5. Een moderne UI-stack gebruikt die schaalbaar is (React 19 + WebGL)

De keuze is niet greenfield bouwen, maar Zeus evolueren tot productierijpheid terwijl de Thetis feature-backlog systematisch wordt ingehaald.

---

## 5. Aanbevolen stack

```
┌─────────────────────────────────────────────────────┐
│  UI Layer                                           │
│  React 19 + TypeScript + WebGL (spectrum/waterfall) │
│  Capacitor (iOS/Android, optioneel Fase 3)          │
├─────────────────────────────────────────────────────┤
│  Application / Backend Layer                        │
│  C# .NET 10 (cross-platform)                        │
│  ASP.NET Core (WebSocket/REST API voor UI)          │
│  Plugin-systeem: .NET MEF of eigen IPlugin<T>       │
├─────────────────────────────────────────────────────┤
│  DSP Layer                                          │
│  WDSP (native C, P/Invoke of CsoundNet wrapper)     │
│  IDspEngine interface (abstractie)                  │
├─────────────────────────────────────────────────────┤
│  Hardware Abstraction Layer                         │
│  IRadioDevice interface                             │
│  OpenHPSDR Protocol 1 assembly                      │
│  OpenHPSDR Protocol 2 assembly                      │
│  (toekomst: SoapySDR bridge voor ander hw)          │
├─────────────────────────────────────────────────────┤
│  Audio Layer                                        │
│  miniaudio (crossplatform, C, single-header)        │
└─────────────────────────────────────────────────────┘
```

**Motivatie per keuze:**

- **.NET 10**: Active LTS, native AOT beschikbaar, uitstekende performance voor netwerkcode en business-logica, grote ecosysteem voor amateur-radio community (C# is dominant in die community)
- **React 19 + TypeScript**: Grootste frontend-ecosysteem, WebGL mogelijk voor spectrum, Capacitor voor mobiel hergebruik van codebase
- **WDSP**: Bewezen radiokwaliteit (noise reduction, AGC, CW-filtering), al geïntegreerd in deskHPSDR en Thetis, open-source (GPL)
- **miniaudio**: Single-header, geen externe dependencies, werkt op Windows/Linux/macOS/iOS/Android, lage latency

---

## 6. Roadmap overzicht

### MVP (Fase 1) — Kernfunctionaliteit, ~6 maanden

Doel: Een werkende, stabiele transceiver op basis van Zeus.

- [ ] Protocol 1 + Protocol 2 stabiel en getest
- [ ] Basisdemodulatie: SSB (USB/LSB), AM, FM, CW
- [ ] Spectrum + waterval (WebGL, minimaal 1 RX paneel)
- [ ] Audio via miniaudio (RX audio out, TX mic in)
- [ ] PTT (via software en CAT basis)
- [ ] Config persistentie (JSON)
- [ ] Windows + Linux builds (CI/CD via GitHub Actions)
- [ ] Basisdocumentatie

### Fase 2 — Feature Parity met Thetis (core), ~6 maanden

Doel: De meest gebruikte Thetis-features implementeren.

- [ ] CAT-protocol (Kenwood TS-2000 emulatie, minimaal commandoset)
- [ ] Multi-RX (2+ gelijktijdige ontvangers)
- [ ] TX DSP: compressor, EQ, VOX
- [ ] N1MM Logger UDP-integratie
- [ ] TCI-protocol (basic)
- [ ] Uitgebreid panadapter-beheer (meerdere RX op paneel)
- [ ] Plugin API stabiel en gedocumenteerd
- [ ] macOS build

### Fase 3 — Differentiatie, ~6-12 maanden

Doel: Functies die Thetis niet had, plus mobiel.

- [ ] Capacitor mobile app (iOS/Android remote control)
- [ ] SoapySDR device bridge (hardware onafhankelijkheid)
- [ ] Remote headless mode (appsrv-patroon van SDRangel)
- [ ] Discord/community integraties
- [ ] AI-assisted signal identification (optioneel)
- [ ] Volledige TCI-implementatie
- [ ] WSPR/FT8 decoder plugin

---

## 7. Top 5 concrete aanbevelingen

**Aanbeveling 1: Adopteer SDRangel's DeviceSet + ChannelAPI patroon**

SDRangel's model van `DeviceSet` (één device met eigen DSP-engine) waaraan meerdere `ChannelAPI`-plugins hangen is het meest volwassen open-source model voor dit domein. NovaSdr moet een equivalent .NET-model definiëren: `IDeviceSet` bevat één `IDeviceEngine` en een lijst van `IChannelPlugin`-instanties. Dit maakt multi-RX native mogelijk.

**Aanbeveling 2: Definieer IRadioDevice en IChannelPlugin interfaces als publieke API in v0.2**

Zoals SDR++ zijn `ModuleManager::Instance` interface publiek maakt, moet NovaSdr zijn plugin-interfaces als NuGet-package publiceren. Dit maakt community-bijdragen aan hardware drivers en decoder-plugins mogelijk zonder toegang tot de core.

**Aanbeveling 3: Migreer Thetis' CAT-implementatie naar een losstaande plugin**

De 7K-regel CAT-implementatie in Thetis is de meest gekopieerde feature door contestanten en logging-software integraties. Prioriteer dit als eerste Fase 2 plugin. Gebruik het als proof-of-concept voor de plugin API.

**Aanbeveling 4: Gebruik een WebSocket-API als primaire UI/backend-interface (SDRangel-patroon)**

SDRangel biedt een volledige REST + WebSocket API die zowel de GUI als `sdrangelcli` (web remote) aandrijft. NovaSdr moet ASP.NET Core gebruiken voor dezelfde scheiding: de React frontend communiceert uitsluitend via een gedefinieerde WebSocket/REST API met de C# backend. Dit maakt remote-gebruik gratis.

**Aanbeveling 5: Zorg voor een harde GPL-compliance audit voor WDSP-integratie**

WDSP is GPL. Thetis en deskHPSDR zijn GPL. Zeus is GPL v2+. Een commerciële plugin (bijv. een betaalde decoder) in een GPL-applicatie is niet toegestaan. Definieer nu de licentie-strategie voor de plugin API en documenteer welke interfaces wel/niet onder GPL vallen. Overweeg een LGPL-shell rond de plugin-interface.

---

## 8. Top 5 risico's

| # | Risico | Kans | Impact | Mitigatie |
|---|--------|------|--------|-----------|
| 1 | Zeus v0.1 heeft diepere architecturale problemen die pas zichtbaar worden bij Fase 2 | Middel | Hoog | Grondige code-review van Zeus internals vóór commit aan de roadmap; parallel greenfield spike uitvoeren voor kritieke subsystemen |
| 2 | WDSP GPL-besmetting blokkeert commerciële plugin-modellen | Laag | Hoog | GPL-compliance audit nu (zie Aanbeveling 5); overweeg LGPL-wrapper |
| 3 | React/WebView audio-latency te hoog voor TX use case | Middel | Hoog | Audio nooit via WebView routeren; alle audio via miniaudio in C# backend; UI is puur visualisatie + controle |
| 4 | Community fragmentatie: deskHPSDR-gebruikers adopteren Zeus niet | Middel | Middel | deskHPSDR-committers vroeg betrekken; Linux-first builds prioriteren; zorg dat Protocol 2 minstens zo goed werkt als in deskHPSDR |
| 5 | Plugin API-instabiliteit blokkeert community-bijdragen | Hoog (vroeg stadium) | Middel | Plugin API semver-stabiel markeren pas na Fase 1; gebruik adapter-pattern zodat breaking changes afgeschermd worden |

---

## 9. Referentiedocumenten

- `analysis/01_deskHPSDR_analyse.md` — deskHPSDR broncode-analyse (PROJECT A)
- `analysis/02_referentie_sdrpp_sdrangel.md` — SDR++ en SDRangel architectuuranalyse
- `analysis/03_zeus_analyse.md` — Zeus broncode-analyse (PROJECT B)
- `analysis/04_thetis_analyse.md` — Thetis feature-inventaris (PROJECT C)
- `analysis/14_aanbevelingen_open_vragen.md` — Aanbevelingen en open vragen

---

*Dit document is gegenereerd als onderdeel van de NovaSdr architectuurfase. Het vervangt geen formeel beslisdocument maar dient als input voor de architectuurbespreking.*
