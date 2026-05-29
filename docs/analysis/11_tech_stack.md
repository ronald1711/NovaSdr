# NovaSdr — Fase 11: Aanbevolen Tech Stack
*Gefundeerde stackkeuze | Gegenereerd: 2026-05-29*

---

## Aanbeveling: Zeus-Evolutie — C# .NET 10 + React 19 + TypeScript

---

## 11.1 Evaluatiematrix

Voor elke potentiële stack zijn de criteria beoordeeld op 1-5 (**1=slecht, 5=uitstekend**):

| Criterium | .NET 10 + React | Rust + Tauri | C++ + Qt6 | C++ + Flutter | .NET + Avalonia |
|---|:---:|:---:|:---:|:---:|:---:|
| WDSP integratie | **5** | 3 | **5** | 2 | **5** |
| Realtime DSP | 4 | **5** | **5** | 2 | 4 |
| Audio latency (native) | 4 | **5** | 4 | 2 | 4 |
| Crossplatform desktop | **5** | **5** | 4 | 3 | **5** |
| Mobile/tablet | 4 | 4 | 3 | **5** | 3 |
| UI moderniteit | **5** | 3 | 4 | **5** | 4 |
| Test ecosysteem | **5** | **5** | 3 | 3 | **5** |
| Community/libraries | **5** | 4 | 4 | 3 | 4 |
| Bestaande codebase reuse | **5** | 1 | 1 | 1 | 3 |
| Build complexiteit | 4 | 3 | 3 | 3 | 4 |
| Ham radio community kennis | 4 | 2 | 3 | 1 | 3 |
| Licentie compatibiliteit | **5** | **5** | 3* | **5** | **5** |
| **TOTAAL** | **55** | **45** | **42** | **35** | **50** |

*Qt6 GPL vereist commerciële licentie voor iOS/Android

---

## 11.2 Aanbevolen Stack: Gedetailleerd

### Backend: C# .NET 10

```
Platform: ASP.NET Core .NET 10 (net10.0)
Runtime: Self-contained publish (geen .NET runtime installatie nodig voor eindgebruiker)
Concurrency: async/await + Thread.Priority=Highest voor DSP workers
GC: Workstation GC (ServerGarbageCollection=false, lagere pauzes)
Interop: LibraryImport source-gen (net6+, betere performance dan DllImport)
Native: unsafe blocks enkel in DSP/audio interop laag
```

**Waarom .NET 10:**
- WDSP integratie via P/Invoke: 200+ functies al geïmplementeerd in Zeus (`NativeMethods.cs`)
- Uitstekend crossplatform: Windows, Linux, macOS, ARM64 — allemaal eerste-klas
- `GC.TryStartNoGCRegion()`: realtime DSP segmenten zonder GC pauses
- `Thread.Priority = ThreadPriority.Highest`: DSP thread krijgt CPU voorrang
- `timeBeginPeriod(1)` (Windows): 1ms timer granulariteit (TX pacing vereist dit)
- Rijke NuGet ecosysteem: LiteDB, Serilog, xUnit, SignalR, Photino.NET
- Native AOT (Ahead-of-Time compilation): snellere startup, kleiner binary

**Risico .NET 10:**
- GC pauses bij hoge allocatiesnelheid: gemitigeerd door `GC.TryStartNoGCRegion()` en objectpool
- P/Invoke overhead: ~100-500ns per aanroep (WDSP calls zijn 1024-sample-batch, niet per-sample)

---

### DSP Engine: WDSP (Warren Pratt NR0V) via P/Invoke

```
Library: libwdsp.so (Linux), libwdsp.dylib (macOS), wdsp.dll (Windows)
Versie: WDSP-1.29 (uit deskHPSDR) of WDSP uit Zeus native/wdsp/
Licentie: GPL v2+
FFT backend: FFTW3 (libfftw3.so, libfftw3f.so)
Binding: NativeMethods.cs (200+ [LibraryImport("wdsp")] signatures — Zeus)
Abstraction: IDspEngine interface → WdspDspEngine → SyntheticDspEngine (tests)
```

**Waarom WDSP:**
- Identieke implementatie in alle drie projecten — bewezen, battle-tested
- `FDnoiseIQ.c` (131K regels): uniek EMNR2 algoritme — extern niet beschikbaar
- `calcc.c`: PureSignal 2.0 predistortion — essentieel voor TX kwaliteit
- `wcpAGC.c`: uitstekende AGC — maatstaf in SDR community
- Alternatief: geen enkel gelijkwaardig open-source DSP framework bestaat
- Zeus IDspEngine interface: perfecte abstractie, volledig testbaar

**Niet vervangen door:**
- GNURadio: te complex, geen native P/Invoke model, andere licentie scope
- Liquid-DSP: mist de specifieke HPSDR-functies (PureSignal, advanced NR)
- SoapySDR DSP: geen DSP, enkel hardware abstraction

---

### Audio: miniaudio (via Zeus native/miniaudio/)

```
Library: libminiaudio.so / miniaudio.dll / libminiaudio.dylib
Versie: miniaudio (David Reid, MIT licentie)
Backend: Platform-native (WASAPI op Windows, CoreAudio op macOS, PulseAudio/ALSA op Linux)
Model: Callback-based (realtime-safe, geen allocaties in callback)
Bridge: SpscRing<float> (lock-free) tussen DSP worker en audio callback
```

**Waarom miniaudio:**
- Één API, alle platforms — WASAPI, CoreAudio, ALSA, PulseAudio, OSS, sndio
- Callback model: realtime-safe, geen allocaties, minimale latency
- MIT licentie: geen GPL-conflicten
- Al aanwezig in Zeus: `native/miniaudio/`, `MiniAudioInput.cs`, `MiniAudioOutput.cs`

**Optionele uitbreiding: ASIO (Windows power users)**
- ASIO via cmASIO model (Thetis) als optionele backend plugin
- < 5ms latency op professionele audio interfaces
- Aparte plugin (niet in GPL-kern): ASIO SDK licentie vereist

---

### Frontend: React 19 + Vite + TailwindCSS 4

```
Framework: React 19.0 (RSC, Server Actions, improved Suspense)
Build: Vite 6 (ESM, HMR, tree-shaking)
Taal: TypeScript 5.7 (strict mode)
Styling: TailwindCSS 4 (utility-first, JIT compilation)
State: Zustand 5 (minimal, no reducers/actions)
Rendering: WebGL (custom shaders voor panadapter/waterfall)
Layout: react-grid-layout 2.x (dockable drag-and-drop panels)
Kaart: react-leaflet 5.x (DX spots, greyline overlay)
Icons: lucide-react (consistent icon set)
PWA: Workbox 7 (offline cache, service worker)
Test: Vitest (unit tests), Playwright (e2e)
```

**Waarom React 19 + WebGL:**
- WebGL GPU-accelerated waterfall: 30 fps met 262144-point FFT, geen CPU bottleneck
- React 19: modernste hooks, concurrent rendering
- Zustand: minimal boilerplate, 25+ stores al geïmplementeerd in Zeus
- react-grid-layout: drag-and-drop panels al werkend (`FlexWorkspace.tsx`)
- TailwindCSS 4: consistent design system, geen CSS-in-JS overhead

---

### Desktop Wrapper: Photino.NET

```
Library: Photino.NET 4.0.16 (MIT licentie)
Model: Native webview wrapper (WKWebView op macOS, WebView2 op Windows, WebKit op Linux)
Audio: miniaudio native output (omzeilt browser WebSocket audio latency)
Voordeel over Electron: geen Node.js, geen Chromium gebundeld
```

**Vergelijking:**
| | Photino.NET | Electron | Tauri |
|---|---|---|---|
| Bundlegrootte | ~5 MB | ~150 MB | ~2 MB |
| Runtime | .NET (al aanwezig) | Node.js + Chromium | Rust |
| Platform webview | Native OS | Gebundeld Chromium | Native OS |
| Reeds in codebase | ✓ Zeus | — | — |

---

### Mobile Wrapper: Capacitor 6.2

```
Framework: Capacitor 6.2.0 (MIT licentie)
Platforms: iOS (14+), Android (API 26+)
Voordeel: Zelfde React codebase hergebruiken
Audio model: Mobile = monitoring/controle only (geen TX via mobile)
```

**Mobile beperkingen (bewust gekozen):**
- TX via mobile is architectureel complex (audio latency, PTT via touchscreen)
- NovaSdr mobile = SDR monitoring console + frequentie-/mode-controle
- TX blijft desktop-only in MVP en Fase 2

---

### Database: LiteDB 5.0.21

```
Type: Embedded document database (NoSQL, MongoDB-compatibele query API)
Bestand: ~/.novasdr/novasdr.db (per-user)
Licentie: MIT
Reeds in gebruik: Zeus (50+ store klassen)
```

---

### Protocol Libraries (volledig hergebruiken uit Zeus)

```
Zeus.Protocol1 (GPL v2+):
  Protocol1Client.cs — UDP P1 streaming
  ControlFrame.cs — 1032-byte P1 encoder
  Discovery/ — broadcast discovery

Zeus.Protocol2 (GPL v2+):
  Protocol2Client.cs — UDP P2 streaming
  Discovery/ — P2 broadcast discovery
  P2TelemetryReading.cs — status packets
```

---

### Build Systeem

```
Backend: dotnet publish (self-contained, single-file optie)
Frontend: npm run build (Vite)
Native: CMake (wdsp, miniaudio, zeus-vst-bridge)
CI/CD: GitHub Actions
Packaging:
  Windows: NSIS of WiX installer
  Linux: .deb package (apt) + AppImage
  macOS: .dmg (notarized)
  Docker: headless server mode
```

---

## 11.3 Verworpen Alternatieven

### Rust + Tauri — Verworpen

**Voordelen:**
- Uitstekende realtime garanties (geen GC)
- Minimale binary grootte
- Memory safety by design
- Moderne async ecosystem

**Redenen verwerping:**
1. **WDSP integratie via `unsafe` Rust FFI**: WDSP heeft 200+ functies met complexe C-callback patronen. Rust unsafe FFI is bruikbaar maar vereist extensieve `bindgen` configuratie en safety wrappers. Schatting: 3-6 maanden extra voor FFI-laag alleen.
2. **Geen bestaande codebase**: Alles herschrijven — Zeus's Protocol1, Protocol2, Dsp assemblies zijn weggegooid. Schatting: 12-18 maanden extra.
3. **Geen ham radio community expertise**: OpenHPSDR community werkt in C/C#. Onboarding van nieuwe bijdragers wordt moeilijker.
4. **Tauri WebView audio**: Zelfde browser-audio-latency probleem als Zeus browser mode (>50ms). Native audio in Tauri vereist Rust audio plugin (extra complexiteit).

**Conclusie:** Uitstekende keuze voor greenfield zonder bestaande codebase. Niet optimaal gegeven de constraint "maximale herbruikbaarheid van Zeus".

---

### C++ + Qt6 — Verworpen

**Voordelen:**
- Uitstekende realtime performance
- Qt6 QML: moderne declaratieve UI
- Directe WDSP integratie (geen P/Invoke overhead)
- SDRangel gebruikt Qt6 succesvol

**Redenen verwerping:**
1. **Qt6 Commercial licentie voor iOS/Android**: Qt for Mobile vereist een commerciële licentie (€3.000-15.000/jaar) voor closed-source distributies. Qt LGPL heeft beperkingen voor mobile apps.
2. **Geen bestaande C++ codebase**: Zeus is de beste basis; alle Protocol1/2 code in C# herschrijven naar C++ vereist 6-12 maanden.
3. **QML learning curve**: Het team werkt in C#/TypeScript. QML is een andere paradigmashift.
4. **C++ maintainability**: deskHPSDR toont aan dat pure C/C++ codebases moeilijk onderhoudbaar zijn voor grotere teams zonder C++-experti

**Conclusie:** Uitstekend voor native desktop SDR (SDRangel model). Niet optimaal gegeven de Zeus-investering en mobile requirements.

---

### C++ DSP Core + Flutter UI — Verworpen

**Voordelen:**
- Flutter: uitstekende native mobile UI
- C++ DSP: maximale realtime performance

**Redenen verwerping:**
1. **Flutter voor desktop SDR**: Flutter is onvolwassen voor complexe desktop SDR-toepassingen. WebGL-equivalent ontbreekt voor GPU-waterfall.
2. **Dart FFI complexiteit**: Dart FFI naar C++ WDSP is omslachtig; async callbacks via platform channels zijn niet realtime-safe.
3. **Geen bestaande codebase herbruikbaar**: Volledig greenfield.
4. **Community**: Geen Flutter-expertise in SDR-gemeenschap.

---

### .NET + Avalonia — Serieus alternatief, niet aanbevolen

**Voordelen:**
- Zelfde .NET 10 backend als Zeus
- Avalonia: crossplatform native UI (geen WebView nodig)
- Betere audio latency dan browser mode
- MVVM patroon: goed testbaar

**Redenen niet aanbevelen (boven Zeus-evolutie):**
1. **Zeus frontend weggooien**: React + WebGL frontend van Zeus is ~18 maanden werk. Avalonia vereist volledige UI rewrite naar XAML/Avalonia.
2. **WebGL waterfall**: Avalonia heeft geen directe WebGL-equivalente panadapter — zou custom rendering vereisen.
3. **Mobile**: Avalonia Mobile is experimenteel in 2026 (geen productie-niveau Capacitor-equivalent).
4. **HamDash UX**: React ecosysteem (react-leaflet, react-grid-layout, etc.) heeft rijkere componenten voor ham radio UX.

**Conclusie:** Serieuze optie als je van nul begint met een .NET UI. Niet optimaal gegeven Zeus's React-frontend.

---

## 11.4 Stack Risico's en Mitigaties

| Risico | Ernst | Mitigatie |
|---|---|---|
| .NET GC pauses in DSP hot path | Hoog | `GC.TryStartNoGCRegion()` + object pool + `Span<T>` zero-alloc |
| Browser WebSocket audio latency | Hoog | Primaire audio altijd via miniaudio native; browser = remote monitoring |
| P/Invoke overhead naar WDSP | Midden | Batch-aanroepen (1024 samples/call, niet per-sample); meten met BenchmarkDotNet |
| React bundle grootte groeit | Laag | Vite tree-shaking + lazy loading panels; code splitting per route |
| Capacitor mobile audio | Hoog | Mobile = geen TX; audio via Web Audio API (monitoring only) |
| Photino.NET deprecatie | Laag | Fallback naar browser-mode; Photino.NET actief onderhouden (MIT) |

---

## 11.5 Versie Pinning

```json
{
  "dotnet": "10.0",
  "react": "^19.0.0",
  "typescript": "^5.7.0",
  "vite": "^6.0.0",
  "tailwindcss": "^4.0.0",
  "zustand": "^5.0.0",
  "react-grid-layout": "^2.2.0",
  "capacitor": "^6.2.0",
  "photino.net": "^4.0.0",
  "litedb": "5.0.21",
  "wdsp": "1.29"
}
```

---

## 11.6 Conclusie

De **C# .NET 10 + React 19** stack scoort het hoogst op alle praktische criteria voor NovaSdr:

1. **Maximale herbruikbaarheid**: Zeus's Protocol1, Protocol2, Dsp assemblies, React frontend, WebGL renderer
2. **Bewezen WDSP integratie**: 200+ signatures al geïmplementeerd, getest
3. **Realtime-capable**: GC.TryStartNoGCRegion + Thread.Priority=Highest
4. **Crossplatform first-class**: Windows, Linux, macOS, ARM64 — allemaal ondersteund
5. **Mobile via Capacitor**: Zelfde React codebase, geen extra ontwikkeling
6. **Testbaar**: xUnit + Vitest, SyntheticDspEngine stub
7. **Korte time-to-market**: Geen alles-herschrijven, evolutie van bewezen basis
