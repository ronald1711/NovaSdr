# 05 — UI/UX Analyse

> Vastgestelde bronfeiten. Aanbevelingen gebaseerd op bewezen patronen uit drie projecten.
> Doel: UI/UX-strategie voor NovaSdr met HamDash- en SDR Console-geïnspireerde principes.

---

## Inhoudsopgave

1. [Per project UI overzicht](#1-per-project-ui-overzicht)
2. [Spectrum/waterfall rendering vergelijking](#2-spectrumwaterfall-rendering-vergelijking)
3. [VFO bedieningsmodel vergelijking](#3-vfo-bedieningsmodel-vergelijking)
4. [Menu/settings discoverability](#4-menusettings-discoverability)
5. [Multi-window / docking support](#5-multi-window--docking-support)
6. [Mobile-geschiktheid scores](#6-mobile-geschiktheid-scores)
7. [Gewenste UX-principes voor NovaSdr](#7-gewenste-ux-principes-voor-novasdr)
8. [UI-patronen OVERNEMEN per project](#8-ui-patronen-overnemen-per-project)
9. [UI-patronen VERMIJDEN per project](#9-ui-patronen-vermijden-per-project)
10. [NovaSdr UI Roadmap](#10-novasdr-ui-roadmap)

---

## 1. Per Project UI Overzicht

### 1.1 deskHPSDR — GTK3 + Cairo

| Dimensie | Beoordeling |
|----------|-------------|
| Toolkit | GTK3 (GNOME stack) |
| Taal | C |
| Moderniteit | Gemiddeld — GTK3 is functioneel maar verouderd |
| Touch-geschiktheid | Slecht — geen touch targets, geen gesture support |
| Adaptive layout | Geen — vaste widget-layout |
| Rendering | Cairo 2D (CPU software rasterization) |
| Thema-ondersteuning | GTK3 CSS theming (beperkt) |
| Font-scaling | Via GTK/DPI settings; niet applicatie-bewust |
| Platforms | Linux, macOS |
| DPI awareness | Gedeeltelijk (GTK HiDPI support) |

**Architectuur UI:**
```
Main window (radio.c / main.c)
  ├── VFO display (frequentie digits, GTK labels)
  ├── Band/mode buttons (GTK3 GtkButton grid)
  ├── Panadapter (GtkDrawingArea + Cairo render)
  ├── Waterfall (GtkDrawingArea + Cairo render)
  ├── Meter widget (S-meter, custom Cairo)
  ├── Audio controls (volume, AF gain)
  └── Menu bar (GtkMenuBar, deep nested menus)
```

**Sterkte:** Stabiel, bewezen, alle controls aanwezig.
**Zwakte:** Geen GPU-acceleratie, geen docking, niet touch-ready, verouderd visueel ontwerp.

---

### 1.2 Zeus — React 19 + WebGL

| Dimensie | Beoordeling |
|----------|-------------|
| Toolkit | React 19 + TypeScript |
| Taal | TypeScript / CSS |
| Moderniteit | Hoog — state-of-the-art web tech stack |
| Touch-geschiktheid | Goed — browser touch events; nog niet volledig geoptimaliseerd |
| Adaptive layout | Ja — react-grid-layout responsive dockable panels |
| Rendering | WebGL (GPU-accelerated spectrum + waterfall) |
| Thema-ondersteuning | CSS custom properties; makkelijk themable |
| Font-scaling | Browser-native (em/rem units) |
| Platforms | Browser, Photino.NET desktop, Capacitor mobile |
| DPI awareness | Volledig (CSS devicePixelRatio + WebGL retina) |

**Architectuur UI:**
```
React App (FlexWorkspace.tsx)
  ├── react-grid-layout (dockable panels)
  │     ├── PanadapterPanel (WebGL canvas)
  │     ├── WaterfallPanel (WebGL canvas)
  │     ├── VfoPanel (frequentie display + tuning)
  │     ├── MeterPanel (S-meter, TX meters)
  │     ├── BandPanel (band selectie)
  │     └── [Plugin panels] (dynamisch geladen)
  ├── Zustand 25+ stores (global UI state)
  └── WebSocket client (binary frames 0x11/0x12/0x16)
```

**Sterkte:** GPU-rendering, dockbaar, themable, plugin UI, cross-platform.
**Zwakte:** v0.1 status, WebSocket overhead voor mobile, Zustand store complexity.

---

### 1.3 Thetis — WinForms (GEARCHIVEERD)

| Dimensie | Beoordeling |
|----------|-------------|
| Toolkit | WinForms (.NET 4.8) |
| Taal | C# |
| Moderniteit | Laag — WinForms is Windows-legacy |
| Touch-geschiktheid | Geen — WinForms heeft geen touch primitives |
| Adaptive layout | Geen — pixel-exacte layout, niet responsive |
| Rendering | SharpDX (gearchiveerd) → SkiaSharp (transitie) |
| Thema-ondersteuning | Windows visual styles; geen custom theming |
| Font-scaling | Beperkt DPI-awareness (pre-HDPI era design) |
| Platforms | Windows only |
| DPI awareness | Beperkt (legacy WinForms DPI handling) |

**Architectuur UI:**
```
console.cs (53.983 regels MainForm)
  ├── Alle controls als fields van MainForm
  ├── Spectrum/waterfall (PictureBox + SharpDX/SkiaSharp)
  ├── VFO buttons/knobs (custom drawn)
  ├── Meter displays (custom GDI+ drawing)
  ├── 100+ tabs/panels voor settings
  └── Event handlers voor alles (inline in MainForm)
```

**Sterkte:** Complete feature set, bewezen in productie, ASIO latency.
**Zwakte:** Niet onderhoudbaar, Windows-only, geen touch, GEARCHIVEERD.

---

## 2. Spectrum/Waterfall Rendering Vergelijking

### 2.1 Cairo (deskHPSDR)

```
Rendering pipeline:
  WDSP GetPixels() → float array (dB waarden)
  → Cairo image surface (software buffer)
  → Kleur mapping (C lookup table, CPU)
  → Cairo fill_rect() per pixel-kolom
  → GTK3 DrawingArea composite

Performance:
  CPU-gebonden: alle bewerkingen op één CPU core
  1024-punt spectrum @ 30fps:
    ~3-8ms per frame op moderne CPU (geschat)
  16384-punt spectrum @ 30fps:
    ~40-80ms per frame → niet haalbaar op Cairo

Kwaliteit:
  Sub-pixel rendering via Cairo anti-aliasing
  Kleurdiepte: 8-bit per kanaal (GTK3 beperking)
  
Waterfall scroll:
  Shift implementatie: memcpy van hele pixel buffer
  O(N×H) waar N=breedte, H=hoogte — CPU intensive
```

### 2.2 WebGL (Zeus)

```
Rendering pipeline:
  Server: WDSP GetPixels() → float array → gzip → WS frame 0x11
  Browser: WebSocket onmessage → decompress → Float32Array
  WebGL: texture.subImage2D() → fragment shader → canvas

Fragment shader (vereenvoudigd):
  uniform sampler2D u_spectrum;    // 1D texture met dB waarden
  uniform sampler2D u_colormap;    // 1D gradient texture (Rainbow/Encom/etc.)
  void main() {
      float db = texture2D(u_spectrum, v_texCoord).r;
      float normalized = (db - minDb) / (maxDb - minDb);
      gl_FragColor = texture2D(u_colormap, vec2(normalized, 0.5));
  }

Performance:
  GPU-gebonden: fragment shader parallelisme
  262144-punt spectrum @ 30fps:
    ~1-3ms GPU texture upload + shader (op moderne GPU)
  
Waterfall scroll:
  GPU texture shift: bindFramebuffer → drawArrays op verschoven texcoords
  O(1) GPU operatie — onafhankelijk van waterfall hoogte
  
Kwaliteit:
  Volledige float32 precisie in shader
  Colormap interpolatie: GPU bilinear filtering
  devicePixelRatio aware: retina-kwaliteit rendering
  
Zeus analyzerFftSize=16384 @ 30fps: ~0.5ms GPU ← triviaal
Zeus maxFftSize=262144: haalbaar op dedicated GPU
```

### 2.3 SharpDX → SkiaSharp (Thetis)

```
SharpDX (gearchiveerd):
  DirectX 11 rendering (Windows)
  GPU-accelerated maar via verouderde API
  
SkiaSharp (actueel):
  Skia graphics library (.NET binding)
  Backends: OpenGL, Metal, Vulkan (platform-afhankelijk)
  GPU-accelerated op moderne Windows/macOS
  
Voordeel Skia:
  Kruis-platform (Linux, macOS, Android, iOS)
  Actief onderhouden (Google Skia project)
  
Nadeel voor NovaSdr:
  Skia is 2D graphics; webGL biedt betere shader-controle
  SkiaSharp in Blazor/WASM is complex
```

### 2.4 Rendering Vergelijking Matrix

| Eigenschap | Cairo (A) | WebGL (B) | SkiaSharp (C) |
|-----------|-----------|-----------|--------------|
| GPU-accelerated | Nee | Ja | Ja |
| 16k FFT @ 30fps | Nee (te traag) | Ja | Ja |
| 262k FFT @ 30fps | Nee | Ja (dedicated GPU) | Mogelijk |
| Waterfall scroll O() | O(N×H) CPU | O(1) GPU | O(N×H) of GPU |
| Custom colormap | CPU lookup | GPU shader | CPU/GPU |
| Retina/HiDPI | Gedeeltelijk | Volledig | Ja |
| Cross-platform | Linux/macOS | Overal (browser) | Overal (.NET) |
| Mobile | Nee | Ja | Ja |
| Onderhoud | Actief | Web-standaard | Actief |
| NovaSdr keuze | Nee | **JA** | Fallback optie |

**Conclusie:** WebGL is de correcte keuze voor NovaSdr spectrum/waterfall.

---

## 3. VFO Bedieningsmodel Vergelijking

### 3.1 deskHPSDR VFO

```
Implementatie:
  GTK3 labels voor frequentie digits (7 digits)
  Mouse wheel op label → digit increment/decrement
  Keyboard shortcuts voor tuning stap
  GTK entry voor directe frequentie invoer
  Band buttons (160m, 80m, 40m, ... 70cm)
  
Bediening:
  Scroll wiel op panadapter → VFO afstemmen (klikbaar spectrum)
  Muis-klik op panadapter → spring naar frequentie
  
Sterkte: bewezen interactie, familiar voor ham radio operators
Zwakte: geen touch, niet mobile-friendly, geen gestures
```

### 3.2 Zeus VFO

```
Implementatie (React component):
  VfoPanel.tsx — frequentie display
  Zustand store: vfoStore (frequentie, mode, step, split, etc.)
  
Tuning mechanismen (vermoedelijk, gebaseerd op architectuur):
  Mouse wheel op VFO panel
  Klik-en-drag op panadapter
  Direct input via keyboard
  
react-grid-layout: VFO panel verplaatsbaar door gebruiker
  
Sterkte: dockbaar, themable, plug-uitbreidbaar
Status: v0.1 — exacte VFO interactie details niet volledig gedocumenteerd
```

### 3.3 Thetis VFO

```
Implementatie:
  Custom WinForms VFO control (handmatig getekend)
  Scroll wheel tuning
  Encoder-achtige muis-interactie voor VFO knob simulatie
  Band plan integratie
  Split VFO (VFO A/B) volledig geïmplementeerd
  
Sterkte: meest complete VFO feature set (split, A/B, XIT/RIT)
Zwakte: Windows-only, niet touch-friendly
```

### 3.4 VFO Aanbeveling voor NovaSdr

```
VFO interactiemodel (priority list):
  1. Muis/touch scroll op frequentie display
  2. Direct numeriek invoer (keyboard)
  3. Klik/tap op panadapter voor directe afstemming
  4. Slider of touch-dial voor coarse tuning (mobile)
  5. Encoder hardware control (via MIDI plugin)
  
VFO state model:
  - VFO A (primaire ontvanger)
  - VFO B (secundaire / split TX)
  - RIT (receive incremental tuning)
  - XIT (transmit incremental tuning)
  - SPLIT mode (A=RX, B=TX)
  - LOCK (voorkomt accidentele afstemming)
  - Stap-grootte: 1Hz, 10Hz, 100Hz, 1kHz, 5kHz, 10kHz, 100kHz
```

---

## 4. Menu/Settings Discoverability

### 4.1 deskHPSDR

```
Menustructuur:
  File, Radio, Receiver, Transmitter, Band, Mode, DSP, View, Help
  Genest menu (4 niveaus diep op sommige paden)
  
Problemen:
  - Instellingen verstopt diep in submenu's
  - Geen zoekfunctie
  - Geen "recent used" of favorieten
  - Inconsistente naam-conventie (enkele items zijn afkortingen)
  
Sterkte:
  - Alle features beschikbaar via menu
  - Ham-radio terminologie correct
```

### 4.2 Zeus

```
Instellingsbeheer:
  LiteDB persistence
  React settings panels (vermoedelijk modals of side panels)
  Zustand stores voor UI state
  
v0.1 status — settings UI architectuur niet volledig gedocumenteerd
react-grid-layout panelen geven gebruiker controle over layout
```

### 4.3 Thetis

```
Settings organisatie:
  Tientallen tabbladen in Settings dialoog
  console.cs heeft inline settings-updates naast event handlers
  
Probleem:
  Verouderd WinForms dialog-based settings (modal, blokkerend)
  Geen search, geen inline documentation
  Overweldigend voor nieuwe gebruikers (100+ instellingen)
  
Sterkte:
  Alle mogelijke instellingen aanwezig
  Power users kennen de locatie na gewenning
```

### 4.4 NovaSdr Settings Aanbeveling

```
Ontwerp principes:
  1. Progressive disclosure: beginner ziet weinig, expert kan alles vinden
  2. Contextgevoelig: settings voor DSP verschijnen naast DSP panel
  3. Zoekbaar: global settings search (Ctrl+K of / commando)
  4. Inline help: tooltip of expandable help per instelling
  5. Preset system: "Contest mode", "DX mode", "CW mode" presets
  
Organisatie:
  ┌─────────────────────────────┐
  │ Quick Access Bar             │
  │ [Mode] [Band] [Power] [Ant] │
  └─────────────────────────────┘
  
  Settings sidebar (slide-in):
  ├── Radio (frequentie, mode, band)
  ├── DSP (AGC, NR, NB, EQ, filters)
  ├── Audio (in/out devices, levels)
  ├── TX (mic, compressor, ALC, power)
  ├── Display (colormap, range, fps)
  ├── Interface (layout, shortcuts)
  └── Plugins (activeer/deactiveer)
```

---

## 5. Multi-window / Docking Support

### 5.1 deskHPSDR

```
Multi-window: Beperkt
  Meerdere GTK windows mogelijk (bv. separate waterfall)
  Geen formeel docking framework
  Layout is semi-vaste stapeling van widgets
  
Bewaard layout: nee — opnieuw openen = standaard layout
```

### 5.2 Zeus — react-grid-layout

```
FlexWorkspace.tsx gebruikt react-grid-layout:
  - Panelen zijn drag-and-drop verplaatsbaar
  - Resize handles per panel
  - Layout opgeslagen in Zustand + LiteDB
  - Breakpoints voor responsive layouts (desktop/tablet)
  - Paneel-toevoegen en -verwijderen dynamisch
  
Panel types (vermoedelijk):
  PanadapterPanel, WaterfallPanel, VfoPanel, MeterPanel,
  BandPanel, LogPanel, ClusterPanel, [Plugin panels]
  
Sterkte: meest flexibele layout management van de drie
```

### 5.3 Thetis

```
Multi-window: beperkt
  Aparte forms voor sommige features (Spectral Display, Cat)
  Geen docking framework
  Layout niet door gebruiker aanpasbaar (WinForms fixed layout)
```

### 5.4 NovaSdr Docking Aanbeveling

```
NovaSdr adopteert react-grid-layout (Zeus model) als basis:

Uitbreidingen ten opzichte van Zeus:
  1. Layout presets: "Compact", "Contest", "DX", "Mobile"
  2. Multi-monitor: panels kunnen naar tweede scherm worden getrokken
     (Photino.NET multi-window + browser popout)
  3. Panel pinning: panel vastzetten (niet per ongeluk verplaatsen)
  4. Panel miniaturize: collapse panel naar titelbalk
  5. Layout export/import: JSON layout file deelbaar met community
  
Mobile adaptive:
  Automatisch wisselen naar single-column layout op schermen <768px
  Touch-gestures: swipe tussen panels
  Bottom navigation voor snelle panel-selectie
```

---

## 6. Mobile-geschiktheid Scores

| Dimensie | deskHPSDR | Zeus | Thetis | NovaSdr Doel |
|----------|-----------|------|--------|-------------|
| Touch targets (min 44px) | 0/10 | 6/10 | 0/10 | 9/10 |
| Swipe/gesture support | 0/10 | 5/10 | 0/10 | 8/10 |
| Adaptive layout | 0/10 | 7/10 | 0/10 | 9/10 |
| Font legibility op 6" | 2/10 | 7/10 | 1/10 | 9/10 |
| VFO op touchscreen | 2/10 | 5/10 | 1/10 | 9/10 |
| Spectrum interactie touch | 1/10 | 5/10 | 0/10 | 8/10 |
| Battery/performance | n/a | 6/10 | 0/10 | 8/10 |
| iOS/Android deployment | 0/10 | 7/10 | 0/10 | 8/10 |
| **Totaalscore (80pt max)** | **5/80** | **48/80** | **2/80** | **68/80** |

**Motivatie Zeus score (48/80):**
- Capacitor 6.2 integratie aanwezig — mobile deployment mogelijk
- React frontend is touch-event-aware
- Maar: v0.1 — touch-optimalisatie niet volledig afgerond
- WebSocket naar externe server is een probleem op mobiel (geen localhost server)

**NovaSdr doel (68/80):**
- Ontwerp-first voor tablet (10" als primary mobile form factor)
- Touch-optimale VFO: grote scroll area, haptic feedback via Web API
- Minimum touch target 48×48px voor alle interactieve elementen
- Waterfall: pinch-to-zoom voor frequentiebereik aanpassing

---

## 7. Gewenste UX-principes voor NovaSdr

### 7.1 HamDash-geïnspireerde Principes

**HamDash** is een moderne ham radio dashboard-filosofie gericht op situational awareness:

```
Principe 1: Band Awareness als eerste visueel signaal
  - Huidige band prominent getoond (kleuring, label)
  - Bandcondities (propagatie score, MUF) altijd zichtbaar
  - DX spots geïntegreerd in spectrum (markers op frequentie)
  - Grey line overlay op mini-wereldkaart (optioneel panel)

Principe 2: DX Spots als native feature
  - Cluster spots verschijnen direct als markers in waterfall
  - Klik op spot → spring naar frequentie
  - Spot filtering: per band, per mode, per continent
  - CQ zones als kleur-overlay op spots

Principe 3: Propagatie-bewust UI
  - HF condities dashboard: SFI, SN, K-index, A-index
  - Berekende max DX afstand per band (libsolar equivalent)
  - Visuele indicator "band open/closed" per segment
  - VOACAP-integratie (toekomstige plugin)
```

### 7.2 SDR Console-geïnspireerde Workflows

**SDR Console** (Simon Brown G4ELI) is het referentiepunt voor power-user radio workflows:

```
Principe 4: Meerdere VFO's als eerste-klas feature
  - VFO A + VFO B altijd tegelijk zichtbaar (split view)
  - Memory channels: directe opslag en recall
  - Band plan visualisatie in spectrum (amateur bandgrenzen)
  - Frequency manager: favorieten, bookmarks, scan lists

Principe 5: Opname als ingebouwde functie
  - Baseband IQ opname (direct op disk, ring buffer)
  - Audio opname (gedemoduleerde audio)
  - Opname-indicator altijd zichtbaar
  - Playback: volledig offline replay van IQ bestand

Principe 6: DX Spots als interactief onderdeel
  (zie ook HamDash principe 2)
  - Bandmap: grafische spot-weergave per band
  - Dupecheck via lokale logboek
  - DXCC entity informatie bij spot
```

### 7.3 Touch-friendly (Tablet Operating)

```
Principe 7: Tablet als primary mobile use case (10" scherm)
  - Alle controls bereikbaar met één hand op staand tablet
  - VFO afstemming via grote ronde touch-knop (CSS + touch events)
  - Spectrum tap: tap-and-hold → spot toevoegen / frequentie opslaan
  - PTT: grote rode/groene knop, swipe-to-transmit als veiligheid
  - Bottom navigation: snelle panel-wissel zonder muis

Principe 8: Geen menu's dieper dan 2 niveaus op mobile
  - Settings toegankelijk via swipe-up sheet (mobile bottom sheet pattern)
  - Quick actions via long-press context menu (touch-first pattern)
  - Keine fly-out menu's (niet touch-friendly)
```

### 7.4 Power-user vriendelijk zonder overbelasting

```
Principe 9: Progressive feature disclosure
  Level 1 (beginner):   Spectrum, VFO, Mode, Band, Volume
  Level 2 (gevorderd):  DSP controls, AGC, NR, Filters, Spots
  Level 3 (expert):     WDSP parameters, protocol debug, plugin dev

  Implementatie:
    Interface level selecteerbaar in settings
    Level 1: verborgen controls zijn echt invisible (geen disabled grays)
    Level 3: unlock via "Advanced mode" toggle

Principe 10: Keyboard-first voor power users
  Universal keyboard shortcuts (configurable):
    F1-F8:   directe mode selectie
    Alt+B:   band selectie popup
    Ctrl+F:  direct frequentie invoer
    Space:   PTT (hold = TX, release = RX)
    Up/Down: VFO tuning (stap instelbaar)
    Ctrl+K:  settings zoeken
    Ctrl+S:  log current QSO
```

---

## 8. UI-patronen OVERNEMEN per Project

### 8.1 Van deskHPSDR Overnemen

```
1. Ham-radio terminologie in controls
   "S-Meter", "AGC", "NR", "NB", "CW", "Split" — gebruik de juiste namen
   Ham radio operators verwachten industrie-standaard terminologie

2. Band- en modeknop layout
   Horizontale reeks bandknoppen (160m/80m/.../70cm)
   Mode selectie in compacte button group (LSB/USB/AM/FM/CW/etc.)
   Dit is het universele ham-radio UI patroon

3. Panadapter + waterfall gestapeld
   Panadapter bovenaan (actueel spectrum)
   Waterfall eronder (tijdsverloop)
   Gesplitste hoogte instelbaar (drag divider)

4. S-meter als prominent instrument
   Altijd zichtbaar, groot, analoge of bargraph stijl
   Naaldanalogisch gevoel in digitale vorm (CSS animation)
```

### 8.2 Van Zeus Overnemen

```
1. IDspEngine interface (zie bestand 04)

2. WebGL spectrum/waterfall rendering
   GPU-accelerated, kleurmaps configureerbaar
   Float32 precisie, retina-aware

3. react-grid-layout dockable panels
   FlexWorkspace concept (gebruiker kan layout aanpassen)
   Layout opgeslagen in LiteDB / localStorage

4. Binary WebSocket frame protocol
   0x11 DISPLAY, 0x12 AUDIO, 0x16 TX_METERS
   Efficiënter dan JSON voor high-frequency data

5. Zustand state management (beperkt tot ≤15 stores)
   Duidelijke store boundary per domein
   Selectors optimaliseren voor minimale re-renders

6. SpscRing lock-free audio bridge

7. Plugin UI extensie model
   Plugin kan eigen panel registreren
   Panel verschijnt in react-grid-layout als drag-bare blok

8. LiteDB voor settings persistence
   Embedded NoSQL, geen externe DB server
   Document model is flexibel voor heterogene settings
```

### 8.3 Van Thetis Overnemen

```
1. Feature completeness referentie
   Thetis heeft 15+ jaar features; gebruik als checklist
   CAT command set (CATCommands.cs) als K3S-compatibiliteits referentie

2. HpsdrBoardKind device model
   Bekende hardware board enum als startpunt voor capability model

3. N1MM UDP spectrum protocol
   1500-line N1MM.cs implementatie als protocol documentatie
   NovaSdr implementeert dit als N1MM plugin

4. Discord integratie concept
   clsDiscord.cs toont dat community-integratie gewenst is
   NovaSdr: optionele notification plugin (Discord/Telegram)

5. ASIO support awareness
   cmASIO aanpak als motivatie voor low-latency audio plugin
   NovaSdr: ASIO via miniaudio's WASAPI exclusive mode (dichtste equivalent)

6. Dual-radio/multi-receiver support
   Thetis ondersteunt meerdere ontvangers
   NovaSdr: multi-device plugin architecture (toekomstige fase)
```

---

## 9. UI-patronen VERMIJDEN per Project

### 9.1 Niet overnemen uit deskHPSDR

```
1. Cairo CPU-rendering voor spectrum
   Niet schaalbaar naar grote FFT of hoge FPS
   
2. Vaste widget layout zonder docking
   Gebruikers hebben verschillende schermgroottes en workflows
   
3. GTK3 geneste menu's voor settings
   Diep geneste menus zijn niet touch-friendly en slecht discoverable
   
4. Compile-time feature flags (SATURN, STEMLAB, etc.)
   Runtime plugins zijn beter dan compilatie-varianten
   
5. Globale C struct als UI state carrier
   State moet expliciet beheerd worden (Zustand/Redux patroon)
```

### 9.2 Niet overnemen uit Zeus

```
1. 25+ Zustand stores zonder duidelijke boundaries
   Teveel stores → moeilijk te tracen state flow
   NovaSdr: max 12-15 stores met expliciete ownership

2. Capacitor voor echte mobile radio-use
   WebSocket naar externe server werkt niet betrouwbaar op mobile data netwerken
   NovaSdr: dedicated React Native of Flutter app als toekomstige mobiele strategie
   Of: P2P WebRTC voor lokale verbinding

3. VST3 bridge in MVP
   Te complex, ABI-instabiel, beperkte gebruikers-base
   NovaSdr: eigen audio processing plugin API; VST3 als fase 3 optie
```

### 9.3 Niet overnemen uit Thetis

```
1. 53.983-regel MainForm (god-object)
   Absolute anti-pattern; nimmer herhalen

2. WinForms modal Settings dialogen
   Modale dialogen blokkeren workflow; gebruik side panels / sheets

3. WinForms Timer voor real-time data
   Gebruik geen UI-thread timers voor spectrum/meter updates
   NovaSdr: server-pushed WebSocket frames

4. SharpDX afhankelijkheid (gearchiveerd)
   Nooit afhankelijkheden van niet-onderhouden bibliotheken

5. Runtime C# scripting (Microsoft.CodeAnalysis) in UI
   Security risico, performance overhead, support-nachtmerrie

6. Microsoft.CodeAnalysis als scripting engine
   Vervang door gecontroleerd plugin API

7. Pixel-exacte WinForms layout
   Breekt op HiDPI, breekt op niet-standaard fonts, breekt op 4K
```

---

## 10. NovaSdr UI Roadmap

### 10.1 MVP (Minimum Viable Product) — Fase 1

**Doel:** Een werkende cross-platform SDR applicatie die vertrouwde ham-radio workflows ondersteunt.

```
Verplichte features MVP:
  ┌─────────────────────────────────────────────────────────────┐
  │ NovaSdr MVP Layout (desktop, 1920×1080)                      │
  │                                                             │
  │ ┌─────────────────────────────────┐  ┌──────────────────┐  │
  │ │   Panadapter (WebGL, 16384pt)   │  │ VFO A             │  │
  │ │   [spectrum trace]              │  │ 14.225.000 MHz    │  │
  │ │                                 │  │ USB  [Split]      │  │
  │ ├─────────────────────────────────┤  ├──────────────────┤  │
  │ │   Waterfall (WebGL, scrolling)  │  │ S-Meter           │  │
  │ │   [kleurmap: Rainbow/Encom]     │  │ ███░░░░  S7        │  │
  │ │                                 │  ├──────────────────┤  │
  │ └─────────────────────────────────┘  │ Band: 20m         │  │
  │                                       │ [160][80][40][20] │  │
  │ ┌─────────────────────────────────────────────────────┐  │  │
  │ │ Quick Bar: [Mode] [Filter] [AGC] [NR] [NB] [Vol] [PTT]│  │  │
  │ └─────────────────────────────────────────────────────┘  │  │
  └─────────────────────────────────────────────────────────────┘

MVP feature checklist:
  [x] Hardware discovery (P1/P2)
  [x] WebGL panadapter + waterfall
  [x] VFO A tuning (mouse wheel, direct input)
  [x] Band selection (160m → 70cm)
  [x] Mode selection (LSB/USB/AM/FM/CW)
  [x] AGC on/off/mode
  [x] Bandpass filter (low/high cut)
  [x] Volume control
  [x] S-meter
  [x] PTT (mouse, keyboard)
  [x] Settings persistence (LiteDB)
  [x] TCI server (basis)
  [x] CAT server (basis Kenwood K3S subset)
  [x] Dockable panels (react-grid-layout)
  [x] Donker thema (default)
```

### 10.2 Fase 2 — Ham Radio Ecosystem

**Doel:** Volledige ham-radio workflow ondersteuning, DX en contesting ready.

```
Fase 2 feature set:
  DX & Propagatie:
    [ ] DX cluster TCP client (als plugin)
    [ ] Cluster spots als spectrum markers
    [ ] Propagatie data (SFI, SN, K, A indices)
    [ ] Grey line kaart widget
    [ ] DXCC entity info bij spot selectie
    
  Multi-receiver:
    [ ] VFO B (tweede ontvanger of split TX)
    [ ] Dual panadapter (sub-receiver in spektrum)
    [ ] RIT/XIT controls
    [ ] Band scope (overzichts spectrum, 100kHz+ span)
    
  Logging & Contesting:
    [ ] Built-in QSO log (ADIF export)
    [ ] Dupecheck op VFO frequentie
    [ ] N1MM integratie plugin (UDP spectrum stream)
    [ ] Macro systeem voor CW/voice keying
    
  DSP uitbreidingen:
    [ ] NR3 (RNNoise of libspecbleach integratie)
    [ ] Audio equalizer UI (grafisch)
    [ ] CW decoder (basic DSP decoding)
    [ ] WSJT-X/FT8 audio routing (VAC equivalent, loopback)
    
  Mobile:
    [ ] Tablet-geoptimaliseerde layout (touch VFO knop)
    [ ] Swipe between panels
    [ ] Bottom navigation bar
    [ ] PWA manifest (installeerbaar op Android/iOS via browser)
    
  Plugin ecosystem:
    [ ] Plugin manager UI (installeer/deïnstalleer/update)
    [ ] Community plugin repository
    [ ] Plugin sandbox (beperkte API surface)
```

### 10.3 Fase 3 — Geavanceerde Features

**Doel:** Professionele en experimentele features voor geavanceerde gebruikers.

```
Fase 3 feature set:
  Advanced DSP:
    [ ] VST3 plugin bridge (als optionele plugin)
    [ ] Spectral blanker (P1 interference cancelling)
    [ ] Multi-channel audio routing (virtual cables)
    [ ] SDR playback (IQ file replay)
    [ ] IQ recording (scheduled, ring buffer)
    
  Multi-device:
    [ ] Tweede HPSDR hardware gelijktijdig
    [ ] Gemeenschappelijk VFO tracking (meerdere radios)
    [ ] Diversity receive (gesynchroniseerde ADC's)
    [ ] Split-site remote (server op locatie, client thuis)
    
  Advanced UI:
    [ ] 3D waterfall (WebGL 3D landscape)
    [ ] Multi-monitor bewuste panel placement
    [ ] Layout sharing community (upload/download presets)
    [ ] Touch screen met dedikeerd hardware encoder support
    
  Ecosystem:
    [ ] WSJT-X auto-configuratie
    [ ] VARA HF/FM modem integratie
    [ ] ALE (Automatic Link Establishment) plugin
    [ ] Aprs.fi live integratie (APRS stations op kaart)
    [ ] HamAlert integratie (push notifications)
```

### 10.4 UI Component Architecture (React)

```
NovaSdr frontend component tree (doelarchitectuur):

<App>
  <ThemeProvider>           ← Donker/licht thema, CSS custom properties
    <WebSocketProvider>     ← WebSocket client, binary frame dispatch
      <FlexWorkspace>       ← react-grid-layout root
        <PanadapterPanel>   ← WebGL canvas component
        <WaterfallPanel>    ← WebGL canvas component
        <VfoPanel>          ← Frequentie display + tuning controls
        <MeterPanel>        ← S-meter + TX meters
        <BandPanel>         ← Band selectie knoppen
        <ModePanel>         ← LSB/USB/AM/FM/CW/etc.
        <DspPanel>          ← AGC, NR, NB, Filter sliders
        <ClusterPanel>      ← DX spots lijst (plugin)
        <LogPanel>          ← QSO log (plugin)
        {pluginPanels}      ← Dynamisch geladen plugin UI's
      </FlexWorkspace>
      
      <QuickBar>            ← Altijd-zichtbare actie bar
      <StatusBar>           ← Hardware status, verbinding, freq
      <SettingsSidebar>     ← Slide-in settings panel
      <CommandPalette>      ← Ctrl+K zoekbaar command panel
    </WebSocketProvider>
  </ThemeProvider>
</App>

State management (Zustand, max 12 stores):
  radioStore      ← frequentie, mode, band, VFO A/B
  dspStore        ← AGC, NR, NB, filters, EQ
  txStore         ← PTT, power, ALC, compressor
  meterStore      ← S-meter, TX meters (real-time)
  spectrumStore   ← spectrum data, colormap, range
  hardwareStore   ← device info, capabilities, status
  uiStore         ← layout, theme, sidebar state
  clusterStore    ← DX spots (plugin: dxcluster)
  logStore        ← QSO log (plugin: logger)
  settingsStore   ← persisted settings
  pluginStore     ← active plugins, plugin state
  audioStore      ← audio device selection, volumes
```

---

*Einde bestand 05 — UI/UX Analyse*
