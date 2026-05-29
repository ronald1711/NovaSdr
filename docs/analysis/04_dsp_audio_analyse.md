# 04 — DSP & Audio Analyse

> Vastgestelde bronfeiten. Analyse beperkt tot gedocumenteerde implementaties.
> Doel: DSP- en audio-strategie voor NovaSdr op basis van bewezen componenten.

---

## Inhoudsopgave

1. [WDSP overzicht](#1-wdsp-overzicht)
2. [WDSP integratie per project](#2-wdsp-integratie-per-project)
3. [RX DSP chain — details](#3-rx-dsp-chain--details)
4. [TX DSP chain — details](#4-tx-dsp-chain--details)
5. [DSP buffer sizes en sample rates](#5-dsp-buffer-sizes-en-sample-rates)
6. [Extra DSP in Zeus](#6-extra-dsp-in-zeus)
7. [Audio API vergelijking](#7-audio-api-vergelijking)
8. [Latency metingen/schattingen per stack](#8-latency-metingenschattingen-per-stack)
9. [SIMD/GPU acceleratie](#9-simdgpu-acceleratie)
10. [Aanbevelingen voor NovaSdr](#10-aanbevelingen-voor-novasdr)
11. [IDspEngine als de juiste abstractie](#11-idspengine-als-de-juiste-abstractie)

---

## 1. WDSP Overzicht

### 1.1 Herkomst en Licentie

**WDSP** (Wideband DSP) is een open-source DSP library geschreven door **Warren Pratt NR0V**, oorspronkelijk ontwikkeld als onderdeel van het OpenHPSDR project. Het is de de-facto standaard DSP engine voor de HPSDR hardware-familie.

- **Auteur:** Warren Pratt NR0V
- **Licentie:** GPL v2+
- **Versie (gedocumenteerd):** 1.29 (deskHPSDR), dynamisch in Zeus/Thetis
- **Taal:** C (met FFTW3 als FFT backend)
- **Afhankelijkheden:** FFTW3 (Fastest Fourier Transform in the West, GPL)
- **Repository:** onderdeel van OpenHPSDR-Thetis en afgeleiden

### 1.2 WDSP Architectuurprincipes

WDSP werkt op basis van twee parallelle processing chains:

- **RXA** (Receive A): het complete RX signaalverwerkingspad
- **TXA** (Transmit A): het complete TX signaalverwerkingspad

Elke chain is geïnstantieerd als een numbered "channel" (0, 1, 2, 3 voor meerdere ontvangers). De interface is C-gebaseerd met globale channel-indexed state.

```c
// WDSP API patroon (C interface)
OpenChannel(channel, in_size, dsp_size, out_size, in_rate, dsp_rate, out_rate, type, ...);
SetRXAAGCMode(channel, mode);
SetRXABandpassFreqs(channel, low, high);
fexchange0(channel, I_array, Q_array, error);  // Input samples
fexchange1(channel, I_array, Q_array, error);  // Output samples
CloseChannel(channel);
```

### 1.3 FFTW3 als Backend

WDSP gebruikt FFTW3 voor:
- Bandpass filter design (FIR via overlap-save convolution in frequency domain)
- Spectrum analyzer FFT
- Noise reduction FFT (NR, NR2)
- WOLA (Weighted OverLap-Add) voor spectral subtraction

FFTW3 ondersteunt SIMD automatisch (SSE2, AVX, AVX-512) via compile-time wisdom files.

---

## 2. WDSP Integratie per Project

### 2.1 deskHPSDR — Statisch Gelinkt

```
Integratiemethode: statisch linken
  libwdsp.a gecompileerd en gelinkt in het build process
  Geen runtime-loading; WDSP is onderdeel van het binary
  
Voordelen:
  - Geen deployment risico (single binary)
  - Compiler kan LTO (Link Time Optimization) toepassen
  - Geen versie-mismatch mogelijk

Nadelen:
  - WDSP update = hercompileren + herlinken
  - Geen hot-reload van DSP parameters via library swap
  - Grotere binary size

Abstractielaag: GEEN
  deskHPSDR roept WDSP functies direct aan vanuit:
  - old_protocol.c (P1 receive thread → fexchange0/1)
  - new_protocol.c (P2 receive thread → fexchange0/1)
  - radio.c (channel open/close, parameter setting)
  
Directe aanroepen in C:
  OpenChannel(0, 1024, 1024, 1024, 48000, 48000, 48000, 0, ...);
  fexchange0(0, i_buf, q_buf, &error);
  // ... UI timer triggers spectrum read
  GetPixels(0, 0, pixels, &flag);  // Analyzer FFT resultaat
```

### 2.2 Zeus — IDspEngine Interface + WdspDspEngine

```
Integratiemethode: dynamisch laden via P/Invoke (LibraryImport)
  wdsp.dll / libwdsp.so geladen op runtime
  200+ LibraryImport signatures in WdspDspEngine.cs

Abstractielaag: JA — IDspEngine interface
  interface IDspEngine
  {
      void OpenRxChannel(int channel, int inSize, int dspSize, int outSize,
                         int inRate, int dspRate, int outRate);
      void CloseRxChannel(int channel);
      void ProcessRx(int channel, Span<float> iq);
      ReadOnlySpan<float> GetRxOutput(int channel);
      void SetRxAgcMode(int channel, AgcMode mode);
      void SetRxBandpassFreqs(int channel, double low, double high);
      void SetRxDemodMode(int channel, DemodMode mode);
      void SetRxNoiseReduction(int channel, NrMode mode);
      // ... 200+ methoden
      void OpenTxChannel(int channel, int inSize, int dspSize, int outSize,
                         int inRate, int dspRate, int outRate);
      void ProcessTx(int channel, Span<float> iq);
      void SetTxMode(int channel, TxMode mode);
      // Spectrum
      void GetSpectrumPixels(int channel, Span<float> pixels);
      float GetRxMeter(int channel, MeterType type);
      float GetTxMeter(int channel, MeterType type);
  }
```

**LibraryImport vs DllImport:**
```csharp
// Oud DllImport patroon (Thetis)
[DllImport("wdsp.dll", EntryPoint = "fexchange0")]
private static extern void fexchange0(int channel, float[] I, float[] Q, out int error);

// Nieuw LibraryImport patroon (Zeus, .NET 7+)
[LibraryImport("wdsp", EntryPoint = "fexchange0")]
[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
private static partial void fexchange0(int channel, ref float I, ref float Q, out int error);
// Voordeel: source-generated marshalling, nul runtime overhead, geen boxing
```

### 2.3 Thetis — Directe P/Invoke (dsp.cs)

```
Integratiemethode: klassieke DllImport P/Invoke
  wdsp.dll (Windows DLL), 1061 regels dsp.cs wrapper
  
Abstractielaag: BEPERKT
  dsp.cs is een thin wrapper klasse met static methods
  Geen interface; directe static method calls vanuit console.cs
  
  public static class WDSP
  {
      [DllImport("wdsp.dll")] public static extern void OpenChannel(...);
      [DllImport("wdsp.dll")] public static extern void fexchange0(...);
      // ... 1061 regels DllImport declarations
  }
  
  Aanroep vanuit console.cs:
    WDSP.OpenChannel(0, ...);
    WDSP.fexchange0(0, i_buf, q_buf, ref error);
```

---

## 3. RX DSP Chain — Details

### 3.1 Volledige RX Signaalverwerkingsketen (WDSP RXA)

De volgende keten is gebaseerd op de deskHPSDR documentatie en WDSP source code structuur:

```
ADC → Hardware NCO/DDC → IQ samples (24-bit)
        │
        ▼
┌───────────────────────────────────────────────────────────────────┐
│                    WDSP RXA Channel                                │
│                                                                   │
│  ① Input Stage                                                    │
│     fexchange0(channel, I[], Q[], &error)                         │
│     - IQ normalisatie (24-bit int → float32 ÷ 2^23)              │
│     - Buffer fill tot dsp_size                                    │
│                                                                   │
│  ② Spectrum Analyzer (side-chain)                                 │
│     - FFT analyse (Hann/Kaiser venster)                           │
│     - analyzerFftSize: deskHPSDR configureerbaar                 │
│     - Zeus: analyzerFftSize=16384, analyzerFps=30                │
│     - Output: dB-gekalibreerde pixels voor waterval              │
│                                                                   │
│  ③ AGC (Automatic Gain Control)                                   │
│     Modes: OFF, LONG, SLOW, MEDIUM, FAST, CUSTOM                 │
│     Parameters: threshold, hang, attack, decay, slope            │
│     Implementatie: feedback AGC met look-ahead                   │
│                                                                   │
│  ④ Bandpass Filter (FIR via FFT convolutie)                      │
│     - Overlap-save methode (FFTW3 gebaseerd)                     │
│     - Low cut / high cut (gebruiker instelbaar per mode)         │
│     - Typische bandbreedtes:                                     │
│        SSB: 100–3000 Hz (LSB: 3000–100 Hz gespiegeld)           │
│        CW: ±100–500 Hz rondom nul-slag                          │
│        AM: ±5 kHz                                                │
│        FM: ±8 kHz (smal) / ±75 kHz (breed)                     │
│                                                                   │
│  ⑤ Demodulator                                                    │
│     Modes: AM, SAM, LSB, USB, CWL, CWU, DSB, FM, DRM, SPEC     │
│     AM: envelope detector                                        │
│     SAM: synchronous AM (PLL)                                    │
│     SSB: direct I/Q → real audio                                │
│     CW: Hilbert transform + beat oscillator                      │
│     FM: discriminator (differentiated phase)                     │
│                                                                   │
│  ⑥ Noise Reduction (NR / NR2)                                    │
│     NR: spectral subtraction (LMS adaptive filter)              │
│     NR2: machine learning-geïnformeerde spectral subtraction     │
│          (Warren Pratt's WDSP-NR2 algoritme)                    │
│                                                                   │
│  ⑦ Noise Blanker (NB / NB2 / SNB)                               │
│     NB: time-domain pulse blanker                               │
│     NB2: frequentie-domein blanker                              │
│     SNB: spectral noise blanker                                 │
│                                                                   │
│  ⑧ Equalizer (EQ)                                                │
│     Grafische EQ: 10-band (standaard) of 12-band (EQ12 flag)   │
│     Implementatie: biquad filter cascade                        │
│                                                                   │
│  ⑨ S-meter                                                        │
│     Berekend na AGC, voor demodulatie                           │
│     eenheid: dBm (gecalibreerd per board)                       │
│                                                                   │
│  ⑩ Output Stage                                                   │
│     fexchange1(channel, I[], Q[], &error)                        │
│     Output: mono audio float32 (voor AM/SSB/CW/FM)              │
│     Sample rate: out_rate (typisch 48000 Hz)                    │
└───────────────────────────────────────────────────────────────────┘
        │
        ▼ mono PCM float32 @ 48kHz
Audio output (PortAudio / PulseAudio / miniaudio / NAudio)
```

### 3.2 WDSP RXA Functiegroepen

| Functiegroep | WDSP prefix | Beschrijving |
|-------------|-------------|-------------|
| Channel management | `OpenChannel`, `CloseChannel` | RXA channel lifecycle |
| Sample exchange | `fexchange0`, `fexchange1` | I/O sample buffers |
| AGC | `SetRXAAGC*` | Automatic gain control |
| Bandpass | `SetRXABandpass*` | FIR bandpass filter |
| Notch | `SetRXANotch*` | Automatic notch filter (ANF) |
| Demodulator | `SetRXADemMod*` | Mode selection en parameters |
| Noise reduction | `SetRXANR*`, `SetRXANRST*` | NR, NR2 |
| Noise blanker | `SetRXANB*`, `SetRXASNB*` | NB, NB2, SNB |
| Equalizer | `SetRXAGrphEQ*` | Graphic EQ |
| Meter | `GetRXAMeter` | S-meter waarden |
| Spectrum | `GetPixels`, `SetAnalyzer*` | FFT spectrum output |
| Pan | `SetRXAPan*` | Stereo panning |
| ANF | `SetRXAANF*` | Automatic notch filter |

---

## 4. TX DSP Chain — Details

### 4.1 Volledige TX Signaalverwerkingsketen (WDSP TXA)

```
Microfoon → Audio input (PortAudio / miniaudio)
        │ PCM float32 @ 48kHz
        ▼
┌───────────────────────────────────────────────────────────────────┐
│                    WDSP TXA Channel                                │
│                                                                   │
│  ① Input Stage                                                    │
│     fexchange0(TX_channel, mic_buf, zero_buf, &error)            │
│     - Real mic audio als I-component, Q=0                        │
│                                                                   │
│  ② Mic Equalizer                                                  │
│     Grafische EQ voor microfooncorrectie                        │
│     Identiek aan RX EQ maar op TX pad                           │
│                                                                   │
│  ③ Leveler (Auto Level)                                          │
│     Zachte AGC op het ingangssignaal                            │
│     Voorkomt overdriving van downstream stages                  │
│     Attack/decay instelbaar                                      │
│                                                                   │
│  ④ CFC (Continuous Frequency Compressor)                         │
│     Spectrale compressie over meerdere frequentiebanden          │
│     Warren Pratt's specifieke algoritme voor spraak              │
│     Verbetert speech intelligibility op HF                       │
│     Parameters: preemphasis curve, gain per band               │
│                                                                   │
│  ⑤ Compressor (Speech Processor)                                 │
│     RMS-gebaseerde spraakcompressie                              │
│     Ratio, threshold, attack, release instelbaar                │
│     Beperkt dynamisch bereik voor betere verstaanbaarheid       │
│                                                                   │
│  ⑥ ALC (Automatic Level Control)                                 │
│     Beperkt TX vermogen tot opgegeven maximum                   │
│     Feedback van PA power meting                                │
│     Attack ~1ms, hang, decay instelbaar                         │
│                                                                   │
│  ⑦ Limiter (Hard Clipper)                                        │
│     Absolute begrenzer om DAC overdriving te voorkomen          │
│     Softclipping met overshoottrap                              │
│                                                                   │
│  ⑧ Modulator                                                      │
│     SSB: Weaver methode of Hilbert (afhankelijk van WDSP build) │
│     AM: carrier + sideband generatie                            │
│     FM: directe fasemodulatie                                   │
│     CW: shaped key-clicks via cosine rise/fall                  │
│     Output: I/Q baseband @ dsp_rate                             │
│                                                                   │
│  ⑨ TX Meters (side-chain)                                        │
│     ALC level, compression, EQ drive, PEP, average power       │
│     Zeus: WS frame 0x16 TX_METERS @ 5Hz                        │
│                                                                   │
│  ⑩ Output Stage                                                   │
│     fexchange1(TX_channel, I[], Q[], &error)                     │
│     I/Q @ dsp_rate → upsampling (indien P2)                    │
└───────────────────────────────────────────────────────────────────┘
        │ I/Q float32
        ▼
[P2: CFIR upsampling 48k→96k→192kHz]
        │
        ▼
UDP TX frames → Hardware DAC
```

### 4.2 WDSP TXA Functiegroepen

| Functiegroep | WDSP prefix | Beschrijving |
|-------------|-------------|-------------|
| Channel management | `OpenChannel` (type=1) | TXA channel lifecycle |
| Sample exchange | `fexchange0`, `fexchange1` | Mic in / I/Q out |
| Mic EQ | `SetTXAGrphEQ*` | TX graphic equalizer |
| Leveler | `SetTXALeveler*` | Auto level control input |
| CFC | `SetTXACFCOM*` | Continuous Freq. Compressor |
| Compressor | `SetTXACompressor*` | Speech compressor |
| ALC | `SetTXAALC*` | Automatic level control |
| Limiter | `SetTXALim*` | Hard limiter / clipper |
| Mode | `SetTXAMode` | SSB/AM/FM/CW/etc. |
| Meters | `GetTXAMeter` | ALC, compression, power |

---

## 5. DSP Buffer Sizes en Sample Rates

### 5.1 Zeus DSP Constants (gedocumenteerd)

```
RXA channel (Zeus, van bronfeiten):
  in_size  = 1024  (input buffer, samples van hardware)
  dsp_size = 1024  (WDSP processing buffer)
  out_size = 1024  (output buffer naar audio)
  in_rate  = 48000 Hz
  dsp_rate = 48000 Hz
  out_rate = 48000 Hz

TXA channel — Protocol 2 specifieke sizing:
  in_size  = 512   (mic audio input @ 48kHz)
  dsp_size = 1024  (WDSP processing @ 96kHz, na intern oversample)
  out_size = 2048  (output @ 192kHz, CFIR output)
  in_rate  = 48000 Hz
  dsp_rate = 96000 Hz  (intern 2× upsample voor betere TX kwaliteit)
  out_rate = 192000 Hz (P2 DUC vereiste sample rate)

CFIR = Cascaded FIR upsampler:
  Stage 1: 48kHz → 96kHz (2× upsample, anti-imaging FIR)
  Stage 2: 96kHz → 192kHz (2× upsample, anti-imaging FIR)
  Kwaliteit: lineaire fase FIR, geen aliasing

Spectrum analyzer (Zeus):
  maxFftSize     = 262144  (maximale FFT, voor maximale resolutie)
  analyzerFftSize = 16384   (standaard weergave FFT)
  analyzerFps    = 30       (frames per seconde spectrum update)
```

### 5.2 deskHPSDR Buffer Sizes (gereconstrueerd uit bronfeiten)

```
RXA channel (P1, 48kHz):
  in_size  = 1024 (geschat; P1 512-byte frame bevat ~63 IQ pairs)
  dsp_size = 1024
  out_size = 1024
  Rates: 48kHz (standaard), 96kHz of 192kHz configureerbaar

RXA channel (P2, meerdere DDC):
  Per DDC channel aparte OpenChannel aanroep
  Rates: tot 384kHz op Saturn

Note: deskHPSDR buffer sizes zijn niet direct gedocumenteerd;
waarden gebaseerd op standaard WDSP configuratie voor HPSDR.
```

### 5.3 Buffer Size Implicaties voor Latency

```
Latency per buffer stage = buffer_size / sample_rate

RXA @ 1024/48000:
  1024 ÷ 48000 = 21.33 ms per DSP cycle

TXA P2 CFIR chain:
  Input:  512 ÷ 48000 = 10.67 ms
  Stage1: 1024 ÷ 96000 = 10.67 ms (identiek, verwacht)
  Stage2: 2048 ÷ 192000 = 10.67 ms (identiek, verwacht)
  Total CFIR overhead: ~10.67 ms extra vs directe 48kHz TX

Trade-off:
  Grotere buffers → hogere latency, beter SNR, minder CPU scheduling overhead
  Kleinere buffers → lagere latency, hogere CPU overhead, meer underrun risico
  
NovaSdr aanbeveling: 512 samples @ 48kHz (10.67 ms) als default;
  CW mode: optioneel 256 samples (5.3 ms) voor verlaagde side-tone latency
```

---

## 6. Extra DSP in Zeus

### 6.1 libspecbleach Integratie

Zeus vermeldt integratie van **libspecbleach** — een open-source spectral noise reduction library.

```
libspecbleach (Luis Favre-Bulle, GPL v2+)
  Algoritme: spectral subtraction met noise floor estimatie
  Methoden:
    - STFT-based subtraction
    - Wiener filter optie
    - Automatische noise profiel estimatie

In Zeus context:
  Gebruikt als NR3/NR4 alternatief (WDSP NR2 aanvulling)
  Of als post-processing stage na WDSP RXA output
  Status: NR3/NR4 stubs aanwezig — volledige implementatie onbevestigd
```

### 6.2 VST3 Bridge

```
Zeus plugin systeem bevat een VST3 bridge:
  Doel: gebruik van VST3 audio processing plugins als DSP inserties
  Positie in chain: na WDSP RXA output, voor audio output
                    of als TX pre-processing voor de mic chain
  
VST3 integratie architectuur:
  IZeusPlugin type: "vst3"
  plugin.json manifest: { "type": "vst3", "path": "/path/to/plugin.vst3" }
  ABI: VST3 SDK C++ → .NET P/Invoke wrapper
  
Risico's:
  - VST3 ABI is C++; .NET interop vereist native bridge DLL
  - Plugin threading moet matchen met Zeus DSP thread model
  - GUI sandbox issues bij Photino.NET embedding
```

### 6.3 NR3/NR4 Stubs

```
Status (april 2026): stubs aanwezig, niet geïmplementeerd
NR3 concept: diepere ML-gebaseerde noise reduction
  (vergelijkbaar met RNNoise of DTLN)
NR4 concept: multi-channel / beamforming NR (speculatief)

NovaSdr notie:
  RNNoise (Xiph.Org, C, BSD) is een bewezen ML-NR kandidaat
  Latency: ~20ms; model size: ~100KB
  Kan worden toegevoegd als IDspEngine extension method
```

---

## 7. Audio API Vergelijking

### 7.1 PortAudio (deskHPSDR — macOS, gedeeltelijk Linux)

```
Kenmerken:
  Cross-platform (Windows, macOS, Linux) — maar in deskHPSDR
    alleen gebruikt op macOS (Linux gebruikt PulseAudio)
  API stijl: callback-based (audio thread)
  Backend: Core Audio (macOS), ALSA/JACK (Linux), WASAPI (Windows)
  Latency: afhankelijk van backend; Core Audio: 5–20ms
  
Voordelen:
  - Bewezen in productie, stabiel
  - Goede latency op macOS via Core Audio
  - Lage overhead
  
Nadelen:
  - C library; geen .NET-native
  - Configuratie per-platform verschilt
  - In deskHPSDR gesplit met PulseAudio (twee code-paden)
```

### 7.2 PulseAudio (deskHPSDR — Linux)

```
Kenmerken:
  Linux-specifiek sound server daemon
  API stijl: async callback + blocking modes
  Latency: 20–100ms (server-side buffering)
  
Voordelen:
  - Standaard op Ubuntu/Debian desktop systemen
  - Easy voor normale gebruikers (system audio mixing)
  
Nadelen:
  - Hoge latency t.o.v. JACK of directe ALSA
  - Server daemon vereist (crash = audio onderbreking)
  - Niet geschikt voor professionele audio/CW (<20ms vereist)
  - PipeWire vervangt PulseAudio op moderne Linux distros
```

### 7.3 miniaudio (Zeus — cross-platform)

```
Kenmerken:
  Single-header C library (dr_libs project)
  Backends: WASAPI, DirectSound (Win), Core Audio (macOS),
            ALSA, PulseAudio, JACK (Linux), sndio (BSD), WebAudio
  API stijl: callback-based (audio thread)
  Licentie: Public Domain / MIT
  
Voordelen:
  - Éen library voor alle platforms
  - Nul extra dependencies
  - JACK ondersteuning ingebakken (professionele audio Linux)
  - Laagste configuratieoverhead voor cross-platform
  - Actief onderhouden (David Reid, GitHub)
  - .NET P/Invoke triviaal (enkele native call)
  
Nadelen:
  - Minder configuratie-opties dan PortAudio voor edge-cases
  - WebAudio (browser) backend is experimenteel
  
Latency (typisch):
  WASAPI exclusive: 3–10 ms
  Core Audio: 5–15 ms
  JACK: 1–5 ms (met lage-latency configuratie)
  ALSA: 5–20 ms
  PulseAudio via miniaudio: 20–80 ms
```

### 7.4 NAudio + ASIO (Thetis — Windows only)

```
NAudio 2.3.0:
  .NET audio library voor Windows
  Backends: WASAPI, DirectSound, WaveOut
  Licentie: Microsoft Public License (Ms-PL)
  
cmASIO (Custom ASIO wrapper):
  ASIO (Steinberg Audio Stream I/O)
  Laagste latency op Windows: 1–4 ms
  Vereist ASIO-driver van soundcard fabrikant
  Niet beschikbaar zonder driver (geen Windows inbox ASIO)
  
PortAudio 19.7.0 (Thetis fallback):
  Gebruikt als derde optie na NAudio/ASIO
  
Thetis audio selectie logica:
  1. cmASIO indien ASIO driver aanwezig
  2. NAudio WASAPI indien geen ASIO
  3. PortAudio als fallback

Nadelen voor NovaSdr:
  NAudio = Windows only; niet bruikbaar voor cross-platform NovaSdr
  cmASIO = Windows + driver vereist; niche use case
```

### 7.5 Audio API Vergelijking Matrix

| Eigenschap | PortAudio | PulseAudio | miniaudio | NAudio | cmASIO |
|-----------|-----------|-----------|-----------|--------|--------|
| Cross-platform | Ja | Nee (Linux) | Ja | Nee (Win) | Nee (Win) |
| .NET native | Nee (P/Inv.) | Nee (P/Inv.) | Nee (P/Inv.) | Ja | Nee (P/Inv.) |
| Min. latency | 5ms | 20ms | 1ms (JACK) | 3ms (WASAPI) | 1ms (ASIO) |
| JACK support | Ja | Nee | Ja | Nee | Nee |
| ASIO support | Via plugin | Nee | Nee | Via cmASIO | Ja |
| Mobile | Nee | Nee | Ja (Android/iOS) | Nee | Nee |
| Onderhoud | Actief | Actief | Actief | Actief | Onbekend |
| Dependency | Medium | Linux daemon | Nul | NuGet | ASIO SDK |

---

## 8. Latency Metingen/Schattingen per Stack

### 8.1 End-to-End RX Audio Latency

```
Component breakdown (worst-case / typical):

deskHPSDR (Linux/PulseAudio):
  UDP receive buffer:       ~2 ms
  Frame demux + copy:       ~0.5 ms
  WDSP RXA (1024/48k):      21.3 ms  ← dominant
  Audio output (PulseAudio): 40–80 ms ← problematisch
  Total: 64–104 ms

deskHPSDR (macOS/PortAudio Core Audio):
  UDP receive buffer:       ~2 ms
  Frame demux + copy:       ~0.5 ms
  WDSP RXA (1024/48k):      21.3 ms
  Audio output (Core Audio): 5–15 ms
  Total: 29–39 ms  ← acceptabel

Zeus (cross-platform, miniaudio WASAPI excl.):
  Async UDP receive:         ~3 ms
  P/Invoke overhead (200+ L.I.): ~0.3 ms
  WDSP RXA (1024/48k):      21.3 ms
  SpscRing (lock-free):      <0.1 ms
  miniaudio WASAPI exclusive: 3–10 ms
  WebSocket → browser:       1–3 ms (localhost)
  Total: 29–38 ms  ← vergelijkbaar met deskHPSDR macOS

Zeus (miniaudio JACK, professioneel):
  Total: ~27–32 ms  ← beste Zeus scenario

Thetis (cmASIO):
  UDP receive:              ~2 ms
  WDSP RXA (P/Invoke):      21.3 ms
  cmASIO output:             1–4 ms
  Total: 24–27 ms  ← laagste gemeten (Windows + ASIO hw)
```

### 8.2 CW Latency Vereisten

```
Side-tone latency (eigen CW toon hobbyst horen):
  Acceptabel: <15 ms (niet merkbaar als echo)
  Grens:      15–30 ms (licht storend)
  Onacceptabel: >30 ms (gehoor-motorische dissonantie)

Implicatie voor NovaSdr:
  CW mode: gebruik kleinere audio buffer (256 samples @ 48kHz = 5.3 ms)
  Of: dedicated side-tone oscillator bypasses DSP chain volledig
  DSP chain: 512-sample buffers @ 48kHz = 10.67 ms acceptabel

IQ → audio latency (hardware RX delay) is niet beïnvloedbaar via software:
  ~21ms DSP + minimale audio buffer = ~26ms minimum voor CW RX
  Dit is acceptabel voor praktisch gebruik
```

---

## 9. SIMD/GPU Acceleratie

### 9.1 WDSP / FFTW3 SIMD

```
FFTW3 SIMD ondersteuning:
  - SSE2: standaard ingebakken (x86/x64)
  - AVX: ingebakken bij FFTW3 met --enable-avx
  - AVX-512: experimenteel in FFTW3 3.3.10+
  - NEON: ARM (Apple M-series, Raspberry Pi 4+)

WDSP profiteert automatisch van FFTW3 SIMD:
  - Bandpass filter convolutie (grootste FFT operations)
  - Analyzer FFT
  - NR2 spectrale subtractie

Verwachte speedup via SIMD:
  SSE2 → AVX: ~1.5–2× op grote FFT's (16k+)
  Praktisch effect bij 48kHz: minimaal (FFT is niet het bottleneck)
  Effect bij 384kHz + 16k analyzer: merkbaar (~15% CPU besparing)

Status per project:
  deskHPSDR: FFTW3 gecompileerd met host-optimale flags (makefile)
  Zeus:      wdsp.dll / libwdsp.so gecompileerd buiten Zeus project
  Thetis:    wdsp.dll Windows build, SSE2 standaard
```

### 9.2 GPU Acceleratie — Spectrum Rendering

```
deskHPSDR (Cairo):
  CPU-only rendering; geen GPU acceleratie
  Cairo gebruikt software rasterization standaard
  GTK3 kan optioneel OpenGL backend gebruiken (zeldzaam geconfigureerd)
  
Zeus (WebGL):
  Volledige GPU acceleratie voor panadapter en waterfall
  WebGL shaders voor:
    - Gradient colormap toepassing (dB → kleur)
    - Waterfall scroll (GPU texture shift)
    - FFT pixel normalisatie
  analyzerFftSize=16384 punten × 30fps = GPU-beheerbaar
  maxFftSize=262144: maximale resolutie, hogere GPU texture overhead
  
Thetis (SharpDX → SkiaSharp):
  SharpDX: DirectX 11 acceleration (gearchiveerd library)
  SkiaSharp: GPU-accelerated (Skia gebruikt OpenGL/Metal/Vulkan)
  Overgang SharpDX→SkiaSharp: technische schuld in transitie

GPU acceleratie voordelen (Zeus WebGL model):
  - Waterfall scroll: O(1) GPU texture shift vs O(N) CPU memcpy
  - Colormap: parallelle shader pixels vs sequentiële CPU loop
  - 30fps update van 16384-punt spectrum: <2ms GPU vs 10ms CPU
```

### 9.3 SIMD voor NovaSdr Aanbeveling

```
NovaSdr hoeft geen eigen SIMD te schrijven:
  1. WDSP + FFTW3 levert al SIMD-optimale DSP
  2. WebGL (React frontend) levert GPU-accelerated rendering
  3. miniaudio heeft NEON/SSE2 geoptimaliseerde resamplers

Toekomstige overweging:
  Als WDSP vervangen wordt door eigen DSP engine:
    Gebruik System.Numerics.Vector<float> (.NET SIMD API)
    Of: System.Runtime.Intrinsics.X86.Avx / AdvSimd (ARM)
    Of: native C library met AVX2 (via P/Invoke)
```

---

## 10. Aanbevelingen voor NovaSdr

### 10.1 DSP Engine: WDSP behouden (kortetermijn)

**Beslissing:** NovaSdr gebruikt WDSP 1.29+ via IDspEngine interface (Zeus model).

**Rationale:**
1. WDSP is bewezen over 15+ jaar HPSDR deployment
2. Warren Pratt's DSP kwaliteit (NR2, CFC, SAM) is niet eenvoudig te repliceren
3. De IDspEngine interface stelt ons in staat WDSP later te vervangen zonder API-wijziging
4. Alternatieve DSP engines (GNU Radio, liquid-dsp) missen de ham-radio-specifieke processing

**Risico's:**
- WDSP is GPL v2; NovaSdr moet GPL-compatibel zijn (geen probleem bij GPL v2+ keuze)
- WDSP is C; .NET P/Invoke vereist native library deployment
- WDSP update-strategie: houd wdsp.dll / libwdsp.so als deployment artifact

### 10.2 Audio: miniaudio

**Beslissing:** NovaSdr gebruikt miniaudio als primaire audio backend.

**Rationale:**
1. Enige volledig cross-platform optie (Windows, macOS, Linux, mobile)
2. Nul external dependencies
3. JACK ondersteuning ingebakken (professionele audio gebruikers)
4. Bewezen in Zeus v0.1 — eerder validatie dan NovaSdr zelf
5. SpscRing lock-free bridge patroon overnemen van Zeus

**Latency strategie:**
```
Default: 1024 samples @ 48kHz (21.3ms, stabiel)
CW mode: 256 samples @ 48kHz (5.3ms, hogere CPU overhead)
Professional (JACK): 128 samples @ 48kHz (2.7ms, minimum systeem vereisten)
```

### 10.3 Spectrum Rendering: WebGL

**Beslissing:** WebGL panadapter/waterfall (React frontend), gebaseerd op Zeus model.

**Rationale:**
1. GPU-accelerated: 262144-punt FFT haalbaar bij 30fps
2. Platform-agnostisch (browser, Photino, Capacitor)
3. Cairo (deskHPSDR) is CPU-gebonden en schaalbaar nadeel bij grote schermen

**Implementatie:** Three.js of eigen WebGL shaders voor maximale controle.

### 10.4 Spectrum Update Protocol

```
NovaSdr adopteert Zeus binary WebSocket frame protocol:
  0x11 DISPLAY  @ 30Hz  — spectrum pixels (16384 float32 → gzip compressed)
  0x12 AUDIO    @ 48kHz — audio PCM stream (fallback voor native app)
  0x16 TX_METERS @ 5Hz  — ALC, compression, power meters

Toevoeging:
  0x13 RX_METERS @ 10Hz — S-meter, signal dB
  0x14 BAND_DATA @ 1Hz  — propgatie, DX spots (laag frequent)
  0x15 DEVICE_STATUS @ 0.5Hz — hardware health, temperature
```

---

## 11. IDspEngine als de Juiste Abstractie

### 11.1 Beargumentatie

De `IDspEngine` interface (van Zeus) is de correcte architectural choice voor NovaSdr om de volgende redenen:

**1. Testbaarheid**
```csharp
// Unit test zonder echte hardware of WDSP binary:
public class RadioServiceTests
{
    [Fact]
    public async Task SetMode_Ssb_CallsSetDemodMode()
    {
        var mockDsp = new Mock<IDspEngine>();
        var svc = new RadioService(mockDsp.Object, ...);
        await svc.SetModeAsync(RadioMode.Usb);
        mockDsp.Verify(d => d.SetRxDemodMode(0, DemodMode.Usb), Times.Once);
    }
}
// Impossible zonder interface — je zou WDSP DLL nodig hebben
```

**2. Vervangbaarheid**
```csharp
// Toekomstige vervanging door eigen DSP engine:
services.AddSingleton<IDspEngine>(sp =>
    useLegacyWdsp
        ? new WdspDspEngine(wdspLibPath)
        : new NovaDspEngine()); // Eigen implementatie, zelfde interface
```

**3. Platform isolatie**
```
IDspEngine verbergt het verschil tussen:
- libwdsp.so (Linux)
- wdsp.dll (Windows)
- libwdsp.dylib (macOS)
Platform-specifieke library loading zit alleen in WdspDspEngine constructor.
```

**4. Multi-engine toekomst**
```csharp
// Meerdere DSP engines parallel (twee radio's, elk eigen engine):
var engine0 = new WdspDspEngine(); // Radio 0
var engine1 = new WdspDspEngine(); // Radio 1 (tweede instantie)
// Of: HybridDspEngine (WDSP + RNNoise post-processing)
```

### 11.2 Aanbevolen IDspEngine Interface voor NovaSdr

```csharp
// NovaSdr.Dsp.Abstractions

public interface IDspEngine : IDisposable
{
    // Channel lifecycle
    void OpenRxChannel(int ch, DspChannelConfig config);
    void CloseRxChannel(int ch);
    void OpenTxChannel(int ch, DspChannelConfig config);
    void CloseTxChannel(int ch);

    // RX processing
    void ProcessRx(int ch, ReadOnlySpan<float> iqIn, Span<float> audioOut);

    // TX processing
    void ProcessTx(int ch, ReadOnlySpan<float> micIn, Span<float> iqOut);

    // RX parameters
    void SetRxMode(int ch, RadioMode mode);
    void SetRxBandpass(int ch, double lowHz, double highHz);
    void SetRxAgcMode(int ch, AgcMode mode);
    void SetRxAgcParams(int ch, AgcParameters p);
    void SetRxNoiseReduction(int ch, NrMode mode);
    void SetRxNoiseBlanker(int ch, NbMode mode);
    void SetRxEqualizer(int ch, ReadOnlySpan<float> gainDb);
    void SetRxNotch(int ch, bool enabled, double freqHz, double bwHz);

    // TX parameters
    void SetTxMode(int ch, RadioMode mode);
    void SetTxMicEqualizer(int ch, ReadOnlySpan<float> gainDb);
    void SetTxLeveler(int ch, bool enabled, double maxGainDb);
    void SetTxCompressor(int ch, bool enabled, double thresholdDb, double ratio);
    void SetTxAlc(int ch, bool enabled, double maxGainDb);
    void SetTxCfc(int ch, bool enabled, CfcParameters p);
    void SetTxCarrier(int ch, double levelDb);  // AM/DSB

    // Meters
    float GetRxMeter(int ch, RxMeterType type);
    float GetTxMeter(int ch, TxMeterType type);

    // Spectrum
    void ConfigureAnalyzer(int ch, int fftSize, WindowFunction window, int fps);
    bool TryGetSpectrumPixels(int ch, Span<float> pixelsDb, out int count);

    // Engine info
    DspEngineInfo GetEngineInfo();
}

public record DspChannelConfig(
    int InputSize, int DspSize, int OutputSize,
    int InputRateHz, int DspRateHz, int OutputRateHz,
    DspChannelType Type  // Rx or Tx
);

public enum RadioMode { Usb, Lsb, Am, Sam, Dsb, Cwu, Cwl, Fm, Drm, Spec }
public enum AgcMode { Off, Long, Slow, Medium, Fast, Custom }
public enum NrMode { Off, Nr1, Nr2, Nr3_Stub }  // Nr3 voor toekomst
public enum NbMode { Off, Nb1, Nb2, Snb }
public enum RxMeterType { SMeter, DBm, DBuV, DBov }
public enum TxMeterType { Alc, Compression, EqDrive, PEP, Average, Phase }
public enum WindowFunction { Hann, Kaiser, Blackman, FlatTop }
```

### 11.3 NovaSdr DSP Startup Sequence

```csharp
// NovaSdr RadioService startup (DI-driven)
public class RadioService : BackgroundService
{
    private readonly IDspEngine _dsp;
    private readonly IHardwareAdapter _hw;
    private readonly IAudioEngine _audio;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // 1. Initialiseer DSP channel
        _dsp.OpenRxChannel(0, new DspChannelConfig(
            InputSize: 1024, DspSize: 1024, OutputSize: 1024,
            InputRateHz: 48000, DspRateHz: 48000, OutputRateHz: 48000,
            Type: DspChannelType.Rx));

        // 2. Standaard parameters
        _dsp.SetRxMode(0, RadioMode.Usb);
        _dsp.SetRxBandpass(0, 150, 3000);
        _dsp.SetRxAgcMode(0, AgcMode.Slow);
        _dsp.ConfigureAnalyzer(0, fftSize: 16384, WindowFunction.Hann, fps: 30);

        // 3. Start hardware streaming
        await _hw.StartAsync(new HardwareConfiguration(...), ct);

        // 4. Process loop
        await foreach (var frame in _hw.ReceiveAsync(ct))
        {
            var audioOut = new float[1024];
            _dsp.ProcessRx(0, frame.IqSamples.Span, audioOut);
            _audio.Write(audioOut);

            if (_dsp.TryGetSpectrumPixels(0, spectrumBuffer, out var count))
                await BroadcastSpectrumAsync(spectrumBuffer[..count], ct);
        }
    }
}
```

---

*Einde bestand 04 — DSP & Audio Analyse*
