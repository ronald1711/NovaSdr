# 02 — Architectuuranalyse per Project

> Vastgestelde bronfeiten: deskHPSDR (C/GTK3), Zeus (C#/.NET 10/React 19), Thetis (C#/.NET 4.8/WinForms).
> Doel: architectuurlessen destilleren voor het NovaSdr project.

---

## Inhoudsopgave

1. [PROJECT A — deskHPSDR](#1-project-a--deskhpsdR)
2. [PROJECT B — Zeus](#2-project-b--zeus)
3. [PROJECT C — Thetis](#3-project-c--thetis)
4. [Vergelijkende sectie: lessen voor NovaSdr](#4-vergelijkende-sectie-lessen-voor-novasdr)

---

## 1. PROJECT A — deskHPSDR

### 1.1 Architectuuroverzicht

deskHPSDR is een klassieke C-applicatie gebouwd rondom een enkelvoudige event-loop (GTK3 main loop). De code is georganiseerd in losse `.c`-modules zonder formele laagscheiding. De applicatie beheert hardware-communicatie, DSP, audio en UI in één process zonder strikt gedefinieerde interfaces tussen lagen.

- **Taal/toolkit:** Pure C, GTK3, Cairo, PortAudio/PulseAudio
- **Licentie:** GPL v3
- **Platforms:** Linux, macOS
- **DSP:** WDSP 1.29 statisch gelinkt (libwdsp.a)
- **Netwerk:** UDP sockets, Protocol 1 (old_protocol.c) en Protocol 2 (new_protocol.c)

De architectuur is procedureel: globale state wordt doorgegeven via globale C-structs en pointers. Er is geen dependency-injectie, geen interface-abstractie voor hardware of DSP.

---

### 1.2 Module Dependency Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          main.c / radio.c                           │
│                        (global radio state)                         │
└───────┬──────────┬──────────┬──────────┬──────────┬────────────────┘
        │          │          │          │          │
   ┌────▼───┐ ┌───▼────┐ ┌───▼───┐ ┌───▼──────┐ ┌─▼──────────┐
   │old_    │ │new_    │ │wdsp   │ │audio.c   │ │gtk/        │
   │protocol│ │protocol│ │calls  │ │portaudio/│ │cairo UI    │
   │.c      │ │.c      │ │(DSP)  │ │pulse     │ │waterfall   │
   └────┬───┘ └───┬────┘ └───┬───┘ └──────────┘ └────────────┘
        │         │          │
   ┌────▼─────────▼──┐  ┌────▼─────────────────────────────────┐
   │ UDP socket layer│  │ libwdsp.a (statisch gelinkt)          │
   │ (discovery,     │  │ RXA / TXA / analyzer / AGC / NR etc. │
   │  data exchange) │  └──────────────────────────────────────┘
   └─────────────────┘

Bijkomende modules:
  tci.c ──────────► libwebsockets (TCI server)
  rigctl.c ────────► CAT (network rigctld protocol)
  dxcluster.c ─────► TCP DX cluster verbinding
  iambic.c ────────► CW keyer logic
  libsolar/ ───────► Solar propagation data
  midi.c ──────────► MIDI (compile-time optioneel)
  saturn.c ────────► Saturn/G2 specifieke registers
```

---

### 1.3 Dataflow RX-pad

```
Hardware ADC (HPSDR board)
        │
        │  UDP IQ frames (512 bytes P1 / 1500 bytes P2)
        ▼
 old_protocol.c / new_protocol.c
 ┌──────────────────────────────┐
 │ receive_thread()             │
 │  - UDP recvfrom()            │
 │  - SYNC byte check (0x7F)    │
 │  - frame demux → IQ samples  │
 └──────────┬───────────────────┘
            │ interleaved I/Q float32
            ▼
 ┌──────────────────────────────┐
 │ WDSP RXA pipeline            │
 │  1. fexchange0() — I/Q in    │
 │  2. Analyzer FFT (spectrum)  │
 │  3. AGC                      │
 │  4. Bandpass filter (FIR)    │
 │  5. Demodulator (AM/FM/SSB)  │
 │  6. NR / NB (noise reduc.)   │
 │  7. EQ (graphic equalizer)   │
 │  8. S-meter computation      │
 │  9. fexchange1() — audio out │
 └──────────┬───────────────────┘
            │ mono/stereo PCM float32
            ▼
 ┌──────────────────────────────┐
 │ audio.c                      │
 │  PortAudio (macOS)           │
 │  PulseAudio (Linux)          │
 │  → system audio device       │
 └──────────────────────────────┘

Spectrum side-chain:
  Analyzer FFT result ──► cairo_waterfall.c ──► GTK3 DrawingArea
```

---

### 1.4 Dataflow TX-pad

```
 ┌──────────────────────────────┐
 │ Microfoon input              │
 │  PortAudio / PulseAudio      │
 └──────────┬───────────────────┘
            │ PCM float32
            ▼
 ┌──────────────────────────────┐
 │ WDSP TXA pipeline            │
 │  1. Mic EQ                   │
 │  2. Leveler (auto gain)      │
 │  3. CFC (Continuous Freq.    │
 │     Compression)             │
 │  4. Compressor (speech)      │
 │  5. ALC (auto level ctrl)    │
 │  6. Limiter                  │
 │  7. Modulator (SSB/AM/FM)    │
 │  8. fexchange1() — I/Q out   │
 └──────────┬───────────────────┘
            │ interleaved I/Q float32
            ▼
 old_protocol.c / new_protocol.c
 ┌──────────────────────────────┐
 │ send_thread()                │
 │  - TX pacing (semaphore/     │
 │    timer-based)              │
 │  - frame packing             │
 │  - UDP sendto()              │
 └──────────┬───────────────────┘
            │
            ▼
 Hardware DAC (HPSDR board PA)
```

---

### 1.5 Thread Model / Concurrency

```
Main thread (GTK3 event loop)
  ├── UI rendering / event handling
  ├── wdsp_get_spectrum() polling (Cairo redraws)
  └── Timer callbacks voor meters/S-meter

receive_thread (pthread)
  ├── blocking recvfrom() UDP
  ├── frame parsing (P1/P2)
  └── calls into WDSP fexchange0() ← kritiek: WDSP niet thread-safe in alle versies

send_thread (pthread)
  ├── semaphore-triggered TX frames
  └── WDSP fexchange1() output

audio_thread (PortAudio/PulseAudio callback)
  └── audio buffer drain naar hardware

spectrum_thread (optioneel, soms inline in receive_thread)
  └── analyzer FFT update
```

**Kritische observatie:** Er is minimale synchronisatie tussen threads. Globale structs worden zonder mutex gelezen/geschreven vanuit meerdere threads (UI thread + receive_thread). Race conditions zijn aanwezig maar praktisch zelden reproduceerbaar bij normale samplerates.

---

### 1.6 Latency-gevoelige Onderdelen

| Component | Latency bron | Typische waarde |
|-----------|-------------|-----------------|
| UDP receive → WDSP | Frame size / sample rate | ~21ms (P1, 48kHz, 1024 samples) |
| WDSP DSP keten | FIR filter taps, FFT grootte | 2–8 ms (configuratie-afhankelijk) |
| Audio output buffer | PortAudio buffer grootte | 5–20 ms |
| Spectrum update | Cairo redraw na FFT | 30–60 ms (niet real-time) |
| TX pacing | Semaphore wake-up granulariteit | ±1 ms jitter |

**End-to-end RX latency schatting:** 28–50 ms (acceptabel voor SSB, marginaal voor CW skimmer).

---

### 1.7 Performance Bottlenecks

1. **Cairo waterfall rendering:** Software-rasterized, CPU-gebonden. Bij grote schermen en 30 fps levert dit merkbare CPU-belasting op een enkel core.
2. **WDSP FFT grootte (analyzer):** Standaard configuratie kan analyzer FFT grootten tot 32768 punten gebruiken. Op langzame CPU's is dit een bottleneck.
3. **Single-threaded GTK event loop:** Alle UI-updates, inclusief spectrum, worden geblokkeerd door slow GTK callbacks.
4. **Statisch gelinkte libwdsp.a:** Geen hot-reloading; DSP parameter changes vereisen her-initialisatie van RXA/TXA channels.
5. **UDP socket receive op enkele thread:** Geen zero-copy, geen ring buffer — elke frame wordt gekopieerd via recvfrom.

---

### 1.8 Globale State Problemen

- `radio` struct (radio.h) bevat vrijwel alle state: ontvanger configuratie, TX parameters, hardware state, UI preferences.
- Geen ownership model; elke module kan elke veld direct muteren.
- Compile-time flags (SATURN, STEMLAB, AUTOGAIN, EQ12) creëren meerdere parallelle code-paden die moeilijk samen te testen zijn.
- Geen clear separation tussen "hardware state" en "user preferences" — alles zit samen in één globally-zichtbare struct.

---

### 1.9 Technische Schuld

| Categorie | Beschrijving | Ernst |
|-----------|-------------|-------|
| Thread safety | Geen mutex op globale radio struct | Hoog |
| Abstractielagen | Hardware protocol direct gekoppeld aan DSP | Hoog |
| Platform ifdefs | `#ifdef __APPLE__` / `#ifdef __linux__` door hele codebase | Medium |
| Fout afhandeling | `perror()` + exit() — geen graceful recovery | Medium |
| Compile-time features | 10+ boolean flags; combinatorische test-explosie | Medium |
| Code grootte old_protocol.c | 3396 lijnen monolithisch — moeilijk te unit-testen | Medium |
| Geen unit tests | Nul formele tests in de codebase | Hoog |

---

### 1.10 Platform-afhankelijkheid

- **macOS:** PortAudio, Core Audio beneath
- **Linux:** PulseAudio (of ALSA via PA)
- **Windows:** Niet ondersteund (GTK3 build vereist MSYS2 — niet mainstream)
- `#ifdef __APPLE__` en `#ifdef __linux__` blokken verspreid door audio en protocol code

---

## 2. PROJECT B — Zeus

### 2.1 Architectuuroverzicht

Zeus is een moderne, layered multi-tier applicatie. De architectuur is expliciet gedeeld in een ASP.NET Core backend (server), een React 19 frontend (browser of Photino.NET desktop wrapper) en optioneel een Capacitor 6.2 mobile app. Dit is de meest architectureel geavanceerde van de drie projecten.

- **Backend:** C# .NET 10, ASP.NET Core, WebSocket server
- **Frontend:** React 19 + TypeScript, Zustand 5 state management, WebGL rendering
- **Desktop wrapper:** Photino.NET (cross-platform, lightweight Chromium embedding)
- **Mobile:** Capacitor 6.2 (iOS/Android webview wrapper)
- **DSP:** IDspEngine interface → WdspDspEngine (200+ LibraryImport P/Invoke naar wdsp native library)
- **Audio:** miniaudio (cross-platform C library via P/Invoke)
- **Persistence:** LiteDB embedded NoSQL

---

### 2.2 Module Dependency Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Browser / Photino.NET / Capacitor                 │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    React 19 Frontend                             │    │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │    │
│  │  │ Zustand  │ │ WebGL    │ │ react-   │ │ Vitest            │  │    │
│  │  │ 25+stores│ │ panadapt │ │ grid-lay │ │ unit tests        │  │    │
│  │  │          │ │ waterfall│ │ (docking)│ │                   │  │    │
│  │  └────┬─────┘ └────┬─────┘ └──────────┘ └───────────────────┘  │    │
│  └───────┼────────────┼────────────────────────────────────────────┘    │
│          │ WebSocket  │ binary frames (0x11/0x12/0x16)                   │
└──────────┼────────────┼──────────────────────────────────────────────────┘
           │            │
┌──────────▼────────────▼──────────────────────────────────────────────────┐
│                         ASP.NET Core Server                               │
│                                                                           │
│  ┌─────────────────┐  ┌──────────────────┐  ┌──────────────────────┐    │
│  │ Zeus.Protocol1/ │  │ Zeus.Protocol2/  │  │ Zeus.Server.Hosting/ │    │
│  │ Protocol1Client │  │ Protocol2Client  │  │ Tci/ (3357 lines)    │    │
│  │ (1032-byte ctrl)│  │ (1444-byte frame)│  │ WebSocket TCI server │    │
│  └────────┬────────┘  └────────┬─────────┘  └──────────────────────┘    │
│           │                    │                                          │
│  ┌────────▼────────────────────▼────────────────────────────────────┐   │
│  │                    IDspEngine interface                            │   │
│  │         WdspDspEngine (200+ LibraryImport P/Invoke)               │   │
│  │         DSP constants: RXA 1024@48k, TXA 512→1024→2048 (CFIR)    │   │
│  └────────────────────────────┬─────────────────────────────────────┘   │
│                                │                                          │
│  ┌─────────────────────────────▼──────────────────────────────────────┐  │
│  │  miniaudio (cross-platform audio, P/Invoke)                        │  │
│  │  SpscRing lock-free bridge (DSP thread → audio thread)             │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│                                                                           │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │  Plugin subsystem                                                  │    │
│  │  IZeusPlugin interface, plugin.json manifest, ABI versioning      │    │
│  │  VST3 bridge (plugin type)                                        │    │
│  └──────────────────────────────────────────────────────────────────┘    │
│                                                                           │
│  LiteDB persistence          xUnit test suite                             │
└───────────────────────────────────────────────────────────────────────────┘
           │
           │ UDP (P1/P2 protocol frames)
           ▼
    HPSDR Hardware
```

---

### 2.3 Dataflow RX-pad

```
Hardware ADC
        │
        │ UDP IQ frames
        ▼
 Protocol1Client.cs / Protocol2Client.cs
 ┌──────────────────────────────────────┐
 │ ReceiveLoop (async Task)             │
 │  - UdpClient.ReceiveAsync()          │
 │  - Frame validation                  │
 │  - DDC demux (P2: MAX_DDC=4 streams) │
 │  - IQ samples → RxIqBuffer           │
 └──────────────┬───────────────────────┘
                │ IQ float32 spans
                ▼
 ┌──────────────────────────────────────┐
 │ WdspDspEngine.ProcessRx()            │
 │  IDspEngine.SetRxInput()             │
 │  WDSP RXA chain (via LibraryImport): │
 │   fexchange0 → AGC → BPF → Demod    │
 │   → NR/NB → EQ → SMeter             │
 │   fexchange1 → audio PCM out        │
 └──────────────┬───────────────────────┘
                │
        ┌───────┴───────────────────┐
        │                           │
        ▼                           ▼
 ┌─────────────────┐     ┌──────────────────────────────┐
 │ SpscRing        │     │ Analyzer FFT result           │
 │ (lock-free)     │     │ maxFftSize=262144             │
 │ DSP→audio thread│     │ analyzerFftSize=16384         │
 └────────┬────────┘     │ analyzerFps=30                │
          │              └──────────────┬─────────────────┘
          ▼                             │ binary WS frame 0x11 DISPLAY
 ┌────────────────┐                     ▼
 │ miniaudio      │          ┌──────────────────────────────┐
 │ output device  │          │ WebGL Panadapter/Waterfall    │
 │ WS frame 0x12  │          │ (GPU-accelerated, React)     │
 │ AUDIO (48kHz)  │          └──────────────────────────────┘
 └────────────────┘
```

---

### 2.4 Dataflow TX-pad

```
 ┌──────────────────────────────────────┐
 │ Microfoon input (miniaudio)           │
 └──────────────┬───────────────────────┘
                │ PCM float32
                ▼
 ┌──────────────────────────────────────┐
 │ WdspDspEngine.ProcessTx()            │
 │  TXA chain (P2 buffer sizing):       │
 │   512@48kHz input                    │
 │   → 1024@96kHz (CFIR upsample)       │
 │   → 2048@192kHz (CFIR upsample)      │
 │  Mic EQ → Leveler → CFC              │
 │  → Compressor → ALC → Limiter        │
 │  → Modulator → I/Q out               │
 └──────────────┬───────────────────────┘
                │ I/Q float32 @ 192kHz (P2)
                ▼
 Protocol2Client.cs
 ┌──────────────────────────────────────┐
 │ TransmitLoop (async Task)            │
 │  - semaphore-driven TX pacing        │
 │  - 1444-byte frame packing           │
 │  - UDP sendto hardware               │
 └──────────────┬───────────────────────┘
                ▼
 ┌──────────────────────────────────────┐
 │ TX Meters → WS frame 0x16 (5Hz)      │
 │ ALC / Compression / Power meters     │
 └──────────────────────────────────────┘
```

---

### 2.5 Thread Model / Concurrency

```
ASP.NET Core Kestrel thread pool
  ├── HTTP request handlers (REST API)
  ├── WebSocket accept loop (per client)
  └── Background services (IHostedService)
       ├── RadioService (manages protocol clients)
       ├── SpectrumBroadcastService (30Hz timer → 0x11 frames)
       └── TxMeterService (5Hz timer → 0x16 frames)

Protocol receive Task (per radio)
  ├── UdpClient.ReceiveAsync() (async/await, I/O thread pool)
  ├── Frame parsing + DDC demux
  └── → WdspDspEngine (sync call, DSP thread)

DSP thread (dedicated, pinned)
  ├── ProcessRx() — WDSP RXA
  ├── ProcessTx() — WDSP TXA
  └── SpscRing.Write() (lock-free enqueue naar audio)

Audio thread (miniaudio callback, OS-level)
  └── SpscRing.Read() (lock-free dequeue)
      → miniaudio device buffer fill

React frontend (browser main thread)
  ├── Zustand store updates (via WS message handlers)
  ├── WebGL render loop (requestAnimationFrame, GPU)
  └── UI event handlers
```

**Sterk punt:** De SpscRing lock-free bridge elimineert priority inversie tussen de DSP thread en audio callback. `async/await` op het network I/O pad voorkomt thread-blocking.

---

### 2.6 Latency-gevoelige Onderdelen

| Component | Latency bron | Schatting |
|-----------|-------------|-----------|
| UDP receive → WDSP | Async overhead + frame size | ~15–22 ms |
| WDSP DSP (RXA) | FIR + FFT @ 1024 samples/48kHz | ~21 ms framegebonden |
| SpscRing bridge | Lock-free, near-zero contention | <0.1 ms |
| miniaudio output | Buffer grootte | 5–15 ms |
| WebSocket → browser | Loopback latency | 1–3 ms (localhost) |
| WebGL render | requestAnimationFrame @ 30fps | ~33 ms worst-case |
| TX CFIR upsampling | 512→2048 pipeline | ~5 ms extra |

**End-to-end RX latency schatting:** 40–60 ms (acceptabel; het WebSocket-hop voegt overhead toe t.o.v. native).

---

### 2.7 Performance Bottlenecks

1. **WebSocket frame serialization bij hoge FPS:** 30 Hz spectrum updates × meerdere clients kunnen Kestrel thread pool satureren bij grote FFT frames.
2. **WDSP P/Invoke overhead:** 200+ marshalled calls per DSP cycle. LibraryImport is efficiënter dan DllImport maar nog steeds boundary-crossing overhead.
3. **Zustand 25+ stores:** Niet-geoptimaliseerde selector subscriptions kunnen cascade re-renders veroorzaken in React bij frequente state updates.
4. **WebGL texture upload voor waterfall:** Grote FFT (16384 punten) → GPU texture update moet gebufferd worden; naïeve implementatie veroorzaakt GPU stalls.
5. **LiteDB writes:** LiteDB is single-writer; frequente settings persistence kan I/O contention geven als niet expliciet gebatched.

---

### 2.8 Globale State Problemen

- Zustand stores kunnen circulaire dependencies hebben als niet zorgvuldig ontworpen. Met 25+ stores is dit een reëel risico.
- Server-side: ASP.NET Core DI container beheert RadioService als singleton — threading van state mutations moet expliciet gesynchroniseerd worden.
- Plugin state: plugins delen geen gestandaardiseerde state channel; communicatie verloopt via events of directe service injection, wat koppeling introduceert.

---

### 2.9 Technische Schuld

| Categorie | Beschrijving | Ernst |
|-----------|-------------|-------|
| v0.1 status | April 2026, veel stubs/TODOs verwacht | Hoog |
| NR3/NR4 stubs | Geavanceerde noise reduction niet geïmplementeerd | Medium |
| VST3 bridge | Architectureel complex, ABI stabiliteit risico | Medium |
| Mobile (Capacitor) | WebSocket-over-loopback werkt niet op echte mobile zonder server | Hoog |
| Plugin ABI | Versioning strategie onbekend in detail | Medium |

---

### 2.10 Platform-afhankelijkheid

- **Windows, Linux, macOS:** Ondersteund via .NET 10
- **Mobile (iOS/Android):** Via Capacitor — vereist netwerk-bereikbare server
- **Desktop:** Photino.NET wraps Chromium; OS WebView2/WebKit afhankelijk
- miniaudio: volledig cross-platform, geen extra afhankelijkheden

---

## 3. PROJECT C — Thetis

> **STATUS: GEARCHIVEERD 2 april 2026. Thetis is officieel end-of-life. Analyse is puur als referentie/les.**

### 3.1 Architectuuroverzicht

Thetis is een Windows-only C# .NET 4.8 WinForms applicatie die is gegroeid vanuit OpenHPSDR-Thetis. De architectuur is prototypisch "big ball of mud": één enorm MainForm (console.cs, 53.983 regels), directe P/Invoke naar wdsp.dll, en functionaliteit die direct in event handlers is geïmplementeerd.

- **Taal/toolkit:** C# .NET 4.8, WinForms
- **Licentie:** GPL v2 (dual-license MW0LGE)
- **Platform:** Windows ONLY
- **DSP:** wdsp.dll, P/Invoke via dsp.cs (1061 regels)
- **Audio:** NAudio 2.3.0, cmASIO, PortAudio 19.7.0

---

### 3.2 Module Dependency Diagram

```
┌────────────────────────────────────────────────────────────────────┐
│              console.cs — MainForm (53.983 regels)                  │
│   Bevat: UI, radio control, settings, CAT, CW, meters, alles...    │
└──┬───────┬──────────┬──────────┬───────────┬────────────┬──────────┘
   │       │          │          │           │            │
┌──▼───┐ ┌─▼───────┐ ┌▼────────┐ ┌▼────────┐ ┌▼────────┐ ┌▼────────┐
│dsp.cs│ │Network  │ │CATComm- │ │TCIServer│ │N1MM.cs  │ │clsDis-  │
│1061  │ │IO.cs    │ │ands.cs  │ │.cs      │ │1500 UDP │ │cord.cs  │
│lines │ │1400 UDP │ │7000+    │ │1000 TCI │ │spectrum │ │Discord  │
│P/Inv.│ │socket   │ │Kenwood  │ │lines    │ │stream   │ │.Net bot │
└──┬───┘ └─┬───────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘
   │        │
┌──▼───┐  ┌─▼──────────────────────────────────────────────────────┐
│wdsp. │  │ clsRadioDiscovery.cs (1500 lines P1/P2 auto-detect)    │
│dll   │  └────────────────────────────────────────────────────────┘
│native│
└──────┘
┌────────────────────────────────────────────────────────────────────┐
│ Audio layer:                                                        │
│  NAudio 2.3.0 → WASAPI / DirectSound                               │
│  cmASIO → ASIO low-latency                                         │
│  PortAudio 19.7.0 → fallback                                       │
└────────────────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────────────────┐
│ Rendering:                                                          │
│  SharpDX 4.2.0 (GEARCHIVEERD — DirectX 11)                        │
│  SkiaSharp 3.119.2 (GPU-accelerated 2D, actief)                   │
└────────────────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────────────────┐
│ Microsoft.CodeAnalysis — runtime C# script compilatie              │
└────────────────────────────────────────────────────────────────────┘

Solution projects:
  Thetis, wdsp, ChannelMaster, Midi2Cat, RawInput, cmASIO, portaudio
```

---

### 3.3 Dataflow RX-pad

```
Hardware ADC
        │ UDP frames
        ▼
 NetworkIO.cs
 ┌──────────────────────────────┐
 │ UDP socket receive loop      │
 │  - 1400 lines raw socket code│
 │  - P1/P2 packet exchange     │
 │  - clsRadioDiscovery.cs      │
 │    (P1/P2 auto-detect)       │
 └──────────┬───────────────────┘
            │ IQ samples (byte arrays)
            ▼
 dsp.cs (P/Invoke to wdsp.dll)
 ┌──────────────────────────────┐
 │ WDSP RXA (classic P/Invoke)  │
 │  fexchange0() / RXASetInput  │
 │  AGC, BPF, Demod, NR, EQ    │
 │  fexchange1() → PCM audio   │
 └──────────┬───────────────────┘
            │
     ┌──────┴─────────────────────────┐
     │                                │
     ▼                                ▼
 NAudio/ASIO/PortAudio          SkiaSharp spectrum
 audio output                   waterfall/panadapter
                                 (replaces deprecated
                                  SharpDX)
```

---

### 3.4 Dataflow TX-pad

```
 Microfoon
  └─► NAudio / cmASIO / PortAudio input
           │
           ▼
  dsp.cs → WDSP TXA (P/Invoke)
  Mic EQ → Leveler → CFC → Compressor → ALC → Limiter
  → Modulator → I/Q out
           │
           ▼
  NetworkIO.cs TX path
  → UDP frames → Hardware DAC
```

---

### 3.5 Thread Model / Concurrency

```
UI Thread (WinForms message pump)
  ├── console.cs event handlers (ALLES: settings, meters, waterfall updates)
  ├── InvokeRequired / Invoke() voor cross-thread UI updates
  └── Microsoft.CodeAnalysis scripting (sync, blocks UI!)

Network receive thread (Thread / ThreadPool)
  └── NetworkIO.cs UDP receive → dsp.cs fexchange0()

Audio thread (NAudio/ASIO callback)
  └── dsp.cs fexchange1() → audio device

Timer threads (System.Windows.Forms.Timer — UI thread!)
  ├── S-meter updates
  ├── Waterfall scroll
  └── CAT polling
```

**Kritisch probleem:** WinForms timers lopen op de UI thread. Dit betekent dat meter-updates en waterfall-redraws de message pump blokkeren, resulterend in UI-latentie bij hoge CPU-belasting.

---

### 3.6 Latency-gevoelige Onderdelen

- ASIO via cmASIO biedt de laagste audio latency (typisch 1–4 ms buffer)
- PortAudio fallback: 5–20 ms
- NAudio WASAPI exclusive mode: 3–10 ms
- UI thread blocking door timers en CodeAnalysis: periodicaal 50–200 ms stalls

---

### 3.7 Performance Bottlenecks

1. **console.cs 53.983 regels:** Compiler en JIT kunnen deze klasse niet efficiënt optimaliseren. IL-grootte beïnvloedt JIT tiering.
2. **SharpDX 4.2.0 gearchiveerd:** Replacement SkiaSharp introduceert extra abstraction overhead; overgangsperiode met mixed rendering.
3. **Microsoft.CodeAnalysis runtime scripting:** JIT compilatie van scripts blokkeert de UI thread.
4. **WinForms timer op UI thread:** Alle periodic UI updates blokkeren potentieel de message pump.
5. **7000+ regels CATCommands.cs:** Kenwood-compat CAT met massale switch/case structuur; niet geoptimaliseerd voor high-frequency CAT polling.

---

### 3.8 Globale State Problemen

- console.cs is de god-object: alle state (radio, UI, DSP, network) is hier opgeslagen als private/public fields.
- Geen duidelijke eigenaarschapsgrens; modules communiceren via directe referenties naar console.cs instantie.
- HpsdrBoardKind enum met vele waarden is globaal gedeeld zonder versioning.

---

### 3.9 Technische Schuld

| Categorie | Beschrijving | Ernst |
|-----------|-------------|-------|
| .NET 4.8 | End-of-maintenance; geen moderne async/await patronen | Kritiek |
| WinForms | Geen cross-platform; aflopende Microsoft investering | Kritiek |
| SharpDX gearchiveerd | Dependency op dead library | Kritiek |
| 53.983 lijnen MainForm | Onmogelijk te unit-testen, te refactoren | Kritiek |
| GEARCHIVEERD project | Officieel EOL 2 april 2026 | Kritiek |
| Geen abstractielagen | Hardware, DSP, audio, UI direct gekoppeld | Hoog |

---

### 3.10 Platform-afhankelijkheid

- **Windows exclusief:** WinForms, WASAPI, DirectX, ASIO — allemaal Windows-only
- Geen pad naar cross-platform zonder complete herschrijving

---

## 4. Vergelijkende Sectie: Lessen voor NovaSdr

### 4.1 Architectuurvergelijking Overzicht

| Dimensie | deskHPSDR (A) | Zeus (B) | Thetis (C) |
|----------|--------------|---------|-----------|
| Architectuurpatroon | Procedureel, monolithisch | Layered, microservices-achtig | God-object monoliet |
| Taal | C | C# .NET 10 | C# .NET 4.8 |
| Frontend scheiding | Nee (GTK3 ingebakken) | Ja (React + WS) | Nee (WinForms alles-in-één) |
| DSP abstractie | Nee (directe WDSP calls) | Ja (IDspEngine interface) | Nee (directe P/Invoke) |
| Protocol abstractie | Nee (if/else in protocol files) | Ja (per-protocol klassen) | Gedeeltelijk (NetworkIO.cs) |
| Audio cross-platform | Gedeeltelijk (PA+PulseAudio) | Ja (miniaudio) | Nee (Windows-only) |
| Plugin systeem | Nee | Ja (IZeusPlugin) | Nee |
| Test coverage | Geen | xUnit + Vitest | Geen |
| Spectrum rendering | Cairo (CPU) | WebGL (GPU) | SkiaSharp (GPU) |
| Thread model | Crude pthreads | Async/await + SpscRing | WinForms timers (problematisch) |
| Mobile-geschiktheid | Nee | Ja (Capacitor) | Nee |
| Status | Actief | v0.1 actief | GEARCHIVEERD |

---

### 4.2 Welke Architectuur Leert ons het Meest voor NovaSdr?

**Zeus (PROJECT B) is de primaire architectuurles.** Het demonstreert de correcte richting op vrijwel elk dimensie:

1. **IDspEngine interface** — de juiste abstraction voor DSP. NovaSdr moet dit overnemen en uitbreiden. Het stelt ons in staat WDSP te vervangen door alternatieve DSP engines (eigen C++ engine, libDSP) zonder protocol of UI te raken.

2. **Gescheiden frontend/backend via WebSocket** — stelt NovaSdr in staat meerdere frontend typen te ondersteunen (browser, native desktop, mobile) zonder code-duplicatie in het DSP/protocol domein.

3. **SpscRing lock-free bridge** — het correcte patroon voor DSP-thread naar audio-thread communicatie. Elimineert priority inversie zonder mutex overhead.

4. **miniaudio** — de juiste keuze voor cross-platform audio. Één C-library, nul platform ifdefs in de application layer.

5. **Plugin systeem (IZeusPlugin)** — NovaSdr moet dit overnemen. Het SDRangel channel plugin model is een aanvullende architectuurles (zie bestand 06).

**deskHPSDR (PROJECT A) leert ons:**
- Het correcte Protocol 1 / Protocol 2 frame formaat en timing (praktisch bewezen over jaren).
- De volledige RX/TX WDSP chain volgorde — dit is de referentie-implementatie.
- Compile-time feature flags zijn een anti-pattern; NovaSdr moet runtime-configureerbare plugins gebruiken.

**Thetis (PROJECT C) leert ons wat te VERMIJDEN:**
- God-object MainForm: nimmer een klasse met 50k+ regels.
- WinForms timers op UI thread voor real-time data.
- Geen abstractielagen tussen hardware/DSP/audio/UI.
- Dependency op gearchiveerde libraries (SharpDX).

---

### 4.3 Aanbevolen NovaSdr Architectuur (synthesis)

```
┌─────────────────────────────────────────────────────────────────────┐
│                     NovaSdr Architectuur (doelstelling)              │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Frontend (React 19 + TypeScript)                                    │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │ WebGL spectrum   │ Zustand state   │ Plugin UI extensions   │    │
│  │ Dockable panels  │ (<=15 stores)   │ (lazy-loaded)          │    │
│  └──────────────────────────────────────────────────────────────┘    │
│           │  WebSocket binary frames (display/audio/meters)          │
├───────────▼─────────────────────────────────────────────────────────┤
│  NovaSdr Server (.NET 10 / ASP.NET Core)                             │
│                                                                      │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │ IHardwareAdapter                                             │    │
│  │  ├── Protocol1Adapter (from deskHPSDR P1 reference)         │    │
│  │  └── Protocol2Adapter (from Zeus P2Client)                  │    │
│  └──────────────────┬──────────────────────────────────────────┘    │
│                     │                                                 │
│  ┌──────────────────▼──────────────────────────────────────────┐    │
│  │ IDspEngine                                                   │    │
│  │  └── WdspDspEngine (LibraryImport, from Zeus)                │    │
│  │      RXA 1024@48k / TXA CFIR 512→2048@192k                  │    │
│  └──────────────────┬──────────────────────────────────────────┘    │
│                     │                                                 │
│  ┌──────────────────▼──────────────────────────────────────────┐    │
│  │ IAudioEngine                                                  │    │
│  │  └── MiniaudioEngine (from Zeus)                              │    │
│  │      SpscRing<float> lock-free bridge                        │    │
│  └──────────────────────────────────────────────────────────────┘    │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │ IPluginHost                                                   │    │
│  │  IZeusPlugin-compatible + NovaSdr extensions                 │    │
│  │  Core: TCI, CAT, DXCluster, N1MM, CW, MIDI                  │    │
│  └──────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

**Kernprincipes:**
- Elke laag communiceert via een expliciete interface (geen directe referenties naar implementaties).
- Geen global state buiten de DI container.
- DSP thread is geïsoleerd; UI thread en audio thread communiceren via lock-free queues.
- Plugins zijn de uitbreidingsmechanisme — geen compile-time flags.
- Tests zijn een first-class citizen; elke interface heeft een mock implementatie voor unit tests.

---

*Einde bestand 02 — Architectuuranalyse per Project*
