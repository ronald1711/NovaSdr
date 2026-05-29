# DeskHPSDR - Inventarisatie Documentatie

**Project:** deskHPSDR  
**Locatie:** `/mnt/data/projects/sdrapp_project/sources/deskhpsdr-master/`  
**Status:** Actief ontwikkeling (geforked van piHPSDR in oktober 2024)  
**Licentiering:** GPLv3  
**Copyrighthouder:** Heiko Amft (DL1BZ) 2024-2025, John Melton (G0ORX/N6LYT) 2015

## 1. Projectoverzicht

### Kernkenmerken
- **Taal:** C (ANSI C conform compileerbare)
- **Build Systeem:** GNU Make
- **Platform Support:** 
  - Linux (aanbevolen: Debian Bookworm/Trixie, ALSA/PulseAudio)
  - macOS (Homebrew vereist, PortAudio)
  - WDSP integration via libfftw3
- **UI Framework:** GTK 3.0+ (webkit2gtk-4.0 of 4.1)
- **Doelplatform:** Cross-platform GUI SDR Console voor HPSDR-borden

### Voornaamste Karakteristieken
- Multi-receiver ondersteuning (configurabel aantal DDCs)
- Protocol 1 (OZY/Metis legacy) EN Protocol 2 (nieuw HPSDR protocol) ondersteuning
- MIDI controller integratie
- TCI/RemoteRig interface via WebSockets
- Advanced DSP via WDSP library (v1.29, Warren Pratt NR0V)
- Astronomie data integratie (libsolar)
- Telnet controle (libtelnet)
- CW keyer (iambic, mode A/B)
- VOX/DEXP implementatie
- Spectrum/waterfall display
- Diversity receiver support
- PureSignal (TX feedback)
- Automatic Antenna Tuner (AH-4) ondersteuning
- Radio discovery en network management

---

## 2. Volledige Mapstructuur

```
deskhpsdr-master/
├── Makefile                          (Hoofdmake file, 1220 regels)
├── README.md
├── COPYING                           (GPLv3 licentie)
├── TROUBLESHOOTING.md
├── Notes_if_using_HERMES-Lite-2.md
├── make.config.deskhpsdr.template
├── update_libs.sh
├── COMPILE.linux
├── COMPILE.macOS
├── .gitignore
│
├── src/                              (179 C-bestanden, 177 header-bestanden)
│   ├── main.c                        (Entry point, GUI initialisatie)
│   ├── radio.c                       (Radio statemachine)
│   ├── receiver.c                    (RX chain, multi-receiver support)
│   ├── transmitter.c                 (TX chain, audio input)
│   ├── vfo.c                         (VFO A/B management)
│   ├── protocol.c (abstraction)
│   ├── new_protocol.c                (Protocol 2: ~3000+ regels)
│   ├── old_protocol.c                (Protocol 1: ~3000+ regels)
│   ├── new_discovery.c               (Protocol 2 radio discovery)
│   ├── old_discovery.c               (Protocol 1 radio discovery)
│   ├── discovered.c                  (Discovered radio state)
│   │
│   ├── Audio Stack:
│   ├── pulseaudio.c                  (PulseAudio I/O)
│   ├── audio.c                       (ALSA I/O)
│   ├── portaudio.c                   (macOS PortAudio)
│   │
│   ├── DSP/Signal Processing:
│   ├── filter.c                      (Bandpass filter control)
│   ├── mode.c                        (Demodulation mode management)
│   ├── agc.c (implied in mode)
│   ├── receiver.c                    (RX DSP chain)
│   ├── transmitter.c                 (TX DSP chain)
│   │
│   ├── CW Keyer:
│   ├── cw_engine.c                   (CW timing + tone generation)
│   ├── iambic.c                      (Iambic keyer firmware)
│   │
│   ├── Control Interfaces:
│   ├── rigctl.c                      (Hamlib rigctld protocol)
│   ├── tci.c                         (TCI WebSocket server)
│   ├── tci_audio.c                   (TCI audio streaming)
│   ├── midi2.c / midi3.c             (MIDI CC binding)
│   ├── mac_midi.c                    (macOS CoreMIDI)
│   ├── alsa_midi.c                   (Linux ALSA MIDI)
│   │
│   ├── UI/Display:
│   ├── appearance.c                  (Theme management)
│   ├── css.c                         (GTK CSS styling)
│   ├── toolbar.c                     (Tool palette)
│   ├── menu.c (various)              (Menu system)
│   ├── rx_panadapter.c               (RX FFT display)
│   ├── tx_panadapter.c               (TX FFT display)
│   ├── waterfall.c                   (Waterfall scrolling display)
│   ├── zoompan.c                     (Pan + zoom gesture)
│   ├── meter.c                       (S-meter + power meters)
│   │
│   ├── Optional Features:
│   ├── saturn*.c                     (SATURN/G2 XDMA native support)
│   ├── ozyio.c                       (Legacy USB OZY support)
│   ├── stemlab_discovery.c           (RedPitaya STEMlab detection)
│   │
│   ├── Utility:
│   ├── greyline.c                    (Solar terminator map)
│   ├── ext.c                         (Extension framework)
│   ├── store.c                       (Settings persistence)
│   ├── version.c                     (Build version tracking)
│
├── wdsp-1.29/                        (WDSP DSP library subdirectory)
│   ├── Makefile
│   ├── RXA.h / RXA.c                 (RX Channel: filters, demod, AGC, NR)
│   ├── TXA.h / TXA.c                 (TX Channel: filters, comp, EQ, mod)
│   ├── comm.h                        (WDSP common structures)
│   ├── ... (50+ more core DSP modules)
│
├── libsolar/                         (Solar/astronomical data library)
│   ├── Makefile
│   ├── solar.c / solar.h
│
├── libtelnet/                        (Telnet control library)
│   ├── Makefile
│   ├── telnet.c / telnet.h
│
├── fonts/                            (TTF/OTF font files)
│   ├── ttf/Roboto/
│   ├── ttf/JetBrainsMono/
│   ├── otf/GNU/
│
├── MacOS/                            (macOS-specifieke resources)
│   ├── Info.plist
│   ├── PkgInfo
│   ├── hpsdr.icns
│   ├── rigctld_deskhpsdr
│
├── LINUX/                            (Linux-specifieke files)
│   ├── deskHPSDR.desktop
│   ├── deskHPSDR.desklnk
│   ├── vcable.sh
│   ├── rigctld_deskhpsdr
│
├── Brick-Tools/                      (FPGA Brick toolchain)
├── HL2-Tools/                        (Hermes Lite 2 utilities)
└── release/                          (Release artifacts)
    └── deskhpsdr/
        ├── hpsdr*.png
        ├── radio_icon.png
        └── trx_icon.png
```

---

## 3. Externe Dependencies

### Verplichte Dependencies
| Bibliotheek | Versie | Type | Doeleinde |
|---|---|---|---|
| GTK+ | 3.0+ | System | UI Framework |
| GLib 2.0 | - | System | Event loop, threading |
| GIO | - | System | File/resource I/O |
| WebKit2GTK | 4.0 of 4.1 | System | Web content embedding |
| json-c | - | System | JSON configuration parsing |
| libfftw3 | - | System | FFT (gelinkt met WDSP) |
| libfftw3f | - | System | Float FFT |
| libwebsockets | - | System | WebSocket server (TCI) |
| OpenSSL | - | System | TLS/HTTPS (TCI) |

### Optionele Dependencies (compile-time configureerbaar)
| Optie | Vlag | Dependencies | Functie |
|---|---|---|---|
| PulseAudio (default Linux) | `AUDIO=PULSE` | libpulse, libpulse-simple | RX/TX audio |
| ALSA (alt. Linux) | `AUDIO=ALSA` | libasound | RX/TX audio |
| PortAudio (macOS default) | `AUDIO=PORTAUDIO` | portaudio-2.0, CoreAudio fw | RX/TX audio |
| MIDI | `MIDI=ON` | libasound (Linux) / CoreMIDI (macOS) | MIDI controller I/O |
| TTS (Text-to-Speech) | `TTS=ON` | AVFoundation (macOS) | Voice announcements |
| SATURN Support | `SATURN=ON` | - | SATURN/G2 native XDMA |
| USB OZY | `USBOZY=ON` | libusb-1.0 | Legacy USB OZY radios |
| STEMlab Discovery | `STEMLAB=ON` | libcurl, libxml-2.0 | RedPitaya STEMlab detection |

### Ingebedde Subprojecten (Makefile-beheerd)
| Subproject | Locatie | Doeleinde |
|---|---|---|
| WDSP | `wdsp-1.29/` | DSP engine (RX/TX processing) |
| libsolar | `libsolar/` | Solar/greyline calculations |
| libtelnet | `libtelnet/` | Telnet server voor netwerk controle |

### Compiler-vereisten
- GCC 7.0+ of Clang (macOS)
- GNU Make 4.0+
- Python (voor build scripts)

---

## 4. Protocol Implementatie Details

### Protocol 1 (Legacy "Old Protocol")

**Bestand:** `src/old_protocol.c` (~3000+ regels)  
**Basisinfo:**
- UDP-gebaseerd
- Synchrone command-response per USB Metis/OZY
- Deprecated maar ondersteund voor backward compatibility

**Wire Format - OZY Buffer (512 bytes)**
```
[SYNC SYNC SYNC C0 C1 C2 C3 C4] [TX Audio] [RX Audio]
 0    1    2    3  4  5  6  7    8...     256...512
```

**Control Bytes (C0-C4):**
- `C0` (0x00-0xFF): MOX, preamp, attenuator
- `C1` (0x00-0xFF): Misc flags (dither, random, gain selection)
- `C2` (0x00-0xFF): Config (Penelope/Mercury presence, clock source)
- `C3` (0x00-0xFF): 122.88 MHz/10 MHz clock select, sample rate (0=48k, 1=96k, 2=192k, 3=384k)
- `C4` (0x00-0xFF): Attenuator value (0-31)

**Discovery Ports (OZY/Metis):**
- Control: UDP 1024
- RX Audio: UDP 1025
- TX Audio: UDP 1026

**Sample Rates:** 48, 96, 192, 384 kHz

**Boards Ondersteund:**
- METIS, HERMES, HERMES LITE, HERMES LITE 2 (v40+), GRIFFIN
- OZY (via USB libusb-1.0)

---

### Protocol 2 (New "ETH Protocol")

**Bestand:** `src/new_protocol.c` (~3500+ regels)  
**Basisinfo:**
- UDP Gigabit Ethernet (1Gbps capable)
- Multiple UDP streams (high-priority, receiver data, TX IQ)
- Dynamic sample rate support
- PureSignal feedback capability (2 dedicated DDCs)

**Wire Format - Metis EP2 Frame (1032 bytes)**

**Sequence/Header (8 bytes):**
```
[Seq(4)][0x00][0x00][0x00][0x00]
```

**Frame Structure:**
```
Byte 0-3:   Sequence number (big-endian u32)
Byte 4-5:   Reserved
Byte 6-7:   Command register data
Byte 8-1031: RX IQ pairs (238 pairs @ 4 bytes each = 952 bytes)
```

**Network Ports (Protocol 2):**
| Function | Port | Direction | Content |
|---|---|---|---|
| High Priority Status | 1025 | RX | PTT, PLL lock, exciter, power meters |
| Rx receiver n | 1035+n | RX | IQ samples (DDC n data) |
| TX IQ | 1034 | TX | TX IQ samples |
| TX Microphone | 1032 | TX | Mic audio |

**High-Priority Status Frame (≥60 bytes, parsed bytes 0-19):**
```
+0: PTT/PLL (bit flags)
+2: Exciter power
+10: Forward power (ALEX)
+18: Reverse power (ALEX)
+19: ADC levels, TX FIFO status
```

**Command Frames (Tx control):**
- `C0` (0xAA...): Attenuator/RX frontend
- `C1` (0x5...): VFO frequency (RX side)
- `C2` (0x7...): VFO frequency RX, frequency tune
- `C3` (0x1...): RX 1 frequency (Protocol 2)
- `C4` (0x8...): Mode register
- Repeating pattern for multi-receiver

**DDC (Digital Down Converter) Allocatie (G2/MkII):**
- DDC 0-1: PureSignal RX (PS feedback channels)
- DDC 2+: User RX receivers

**Sample Rates:** 48, 96, 192 kHz

**Boards Ondersteund:**
- ATLAS, HERMES, HERMES 2, ANGELIA, ORION, ORION 2, HERMES LITE, SATURN, HERMES LITE 2
- STEMlab/Hamlab (via protocol wrapper)

**Enums & Structuren:**
```c
enum _device_enum {
    DEVICE_METIS = 0,
    DEVICE_HERMES = 1,
    ...
    DEVICE_HERMES_LITE2 = 506,
    ...
};

typedef struct _DISCOVERED {
    int protocol;  // ORIGINAL_PROTOCOL=0 / NEW_PROTOCOL=1
    int device;    // Enum device type
    int use_tcp;
    char name[64];
    int software_version;
    int fpga_version;
    struct sockaddr_in address;
    // ...
} DISCOVERED;
```

---

## 5. DSP Pipeline Beschrijving

### RX (Receive) Chain - WDSP RXA

**Input → Output Flow (src/receiver.c):**

```
ADC Input (IQ) 
    ↓
[RXA Channel (WDSP)]
    ├─ Shift (CTUN frequency correction)
    ├─ Input Resampler (to common 48 kHz)
    ├─ Bandpass Filter (adaptive cutoff)
    ├─ AGC (Automatic Gain Control, 4 modes: FAST/SLOW/LONG/OFF)
    ├─ Noise Reduction Pipeline:
    │  ├─ NR (Variable-Leak LMS) / NR2 (AEMNR) / Spectral
    │  ├─ Adaptive Notch Filter (ANF)
    │  └─ Spectral Noise Blanker (SNB)
    ├─ Noise Blanker (NB) / Interpolating WB Blanker (NB2)
    ├─ Demodulator (mode-dependent: AM/FM/SSB/CW/DIGU/DIGL)
    ├─ Product Detector (SSB/CW)
    ├─ S-Meter measurement (RMS/peak)
    ├─ Output Resampler (to output rate)
    └─ Audio Output (PCM float32)
        ↓
    Audio Output Buffer → Audio Device (ALSA/PulseAudio/PortAudio)
    Display (Panadapter/Waterfall FFT) ← Tapped from analyzer
```

**WDSP RXA Instellingen (from receiver.h):**
```c
struct _receiver {
    int agc;                   // 0=OFF, 1=FAST, 2=SLOW, 3=LONG
    double agc_gain;
    double agc_slope;
    double agc_hang_threshold;
    double agc_thresh;
    
    int nb;                    // 0=OFF, 1=NB, 2=NB2
    int nr;                    // 0=OFF, 1=NR, 2=NR2, 3=...
    int anf;                   // Automatic Notch: 0=OFF, 1=ON
    int snb;                   // Spectral Noise Blanker: 0=OFF, 1=ON
    int nr_agc;                // Position: 0=before AGC, 1=after AGC
    
    // NR2-specifieke parameters
    int nr2_gain_method;       // 0=GaussianSpeechLin, 1=GaussianSpeechLog, 2=GammaSpeech
    int nr2_npe_method;        // 0=OSMS, 1=MMSE
    int nr2_ae;                // Artifact Elimination: 0=OFF, 1=ON
    
    int filter_low;            // Filter low cutoff (Hz)
    int filter_high;           // Filter high cutoff (Hz)
};
```

---

### TX (Transmit) Chain - WDSP TXA

**Mic Input → RF Output Flow (src/transmitter.c):**

```
Microphone Input (float32 @ 48 kHz)
    ↓
[TXA Channel (WDSP)]
    ├─ Mic Input Meter (peak/average)
    ├─ Input Resampler (to DSP rate: 48/96/192 kHz)
    ├─ Compressor (optional, threshold-based)
    ├─ Continuous Frequency Compression (CFC, 10/12-band EQ)
    ├─ EQ post-processor
    ├─ Leveler (makeup gain, max ~15 dB)
    ├─ Modulator (mode-dependent):
    │  ├─ SSB: Product modulator + 90° phasing
    │  ├─ AM: Envelope modulation
    │  ├─ FM: Phase modulation (deviation control)
    │  └─ CW: Tone shaping + keying
    ├─ Output Resampler (to 48 kHz DAC or protocol rate)
    ├─ TX Meter measurement (output level)
    ├─ PA Drive Control (0-255 amplitude)
    └─ IQ Output (to radio TX port)
        ↓
    TX IQ Ring Buffer → Protocol 1/2 UDP TX port
    [Optional] PureSignal Feedback → Pre-distortion engine
```

**WDSP TXA Instellingen (from transmitter.h):**
```c
struct _transmitter {
    int filter_low;            // TX filter low (Hz)
    int filter_high;           // TX filter high (Hz)
    
    int compressor;            // 0=OFF, 1=ON
    double compressor_level;   // dB
    
    int cfc;                   // Continuous Frequency Compressor: 0=OFF, 1=ON
    int cfc_eq;                // CFC post-EQ: 0=OFF, 1=ON
    double cfc_freq[11];       // 10 corner frequencies
    double cfc_lvl[11];        // Compression level per band
    
    int drive;                 // 0-255 TX amplitude
    int tune_drive;            // TUNE mode drive (lower)
    
    double mic_gain;           // Mic input gain (dB)
    
    int ctcss_enabled;         // CTCSS tone: 0=OFF, 1=ON
    int ctcss;                 // CTCSS frequency index
    
    int deviation;             // FM deviation (Hz)
    
    double am_carrier_level;   // AM carrier as % of peak
};
```

**RX/TX Cross-coupling:**
- S-Meter sourced van RXA meter
- TX panadapter sourced van TXA analyzer (dedicated WDSP instance)
- PureSignal loops TX output back through separate RXA for feedback

---

## 6. Audio Stack

### PulseAudio (Recommended Linux)
**Bestanden:** `src/pulseaudio.c`
- Connects via PulseAudio simple API
- Supports sink/source enumeration
- Automatic reconnect on hot-plug
- Volume control via PA device

### ALSA (Alternative Linux)
**Bestanden:** `src/audio.c`
- Direct ALSA PCM device access
- hw:n,m device name format
- Hardware-level sample rate control
- Lower latency potential

### PortAudio (macOS required)
**Bestanden:** `src/portaudio.c`
- Abstracts CoreAudio on macOS
- Automatic I/O device discovery
- Buffer size negotiation
- Framework dependencies: CoreAudio, AudioToolbox, AudioUnit

**Audio Configuration (radio.h):**
```c
extern int mic_linein;           // 0=Mic, 1=Line-in
extern double linein_gain;       // Input gain in dB
extern int mic_boost;            // Mic preamp: 0=OFF, 1=ON (+20dB)
extern int mic_bias_enabled;     // Phantom power
extern int mic_input_xlr;        // 0=3.5mm, 1=XLR
```

**Ring Buffer Management (new_protocol.c):**
```c
#define TXIQRINGBUFLEN    97920   // ~85 ms TX IQ storage
#define RXAUDIORINGBUFLEN 16384   // ~85 ms RX audio storage

static unsigned char *RXAUDIORINGBUF = NULL;
static unsigned char *TXIQRINGBUF = NULL;
static volatile int txiq_inptr, txiq_outptr, txiq_count;
static volatile int rxaudio_inptr, rxaudio_outptr;
```

---

## 7. UI Architectuur

### GTK 3 + WebKit2GTK Framework

**Hoofd Entry Point:** `src/main.c`
- GtkApplication initialisatie
- Plugin/extension loader
- Main window creation

**Core UI Modules:**
| Module | Bestand | Functie |
|---|---|---|
| Main Display | `main.c`, `console.cs` | Parent window, layout |
| Toolbar | `toolbar.c`, `toolset.c` | Control buttons, sliders |
| VFO Display | `vfo.c`, `vfo_menu.c` | Frequency entry, VFO A/B |
| Panadapter RX | `rx_panadapter.c` | FFT spectrum display |
| Panadapter TX | `tx_panadapter.c` | TX spectrum display |
| Waterfall | `waterfall.c` | Scrolling waterfall (Cairo) |
| S-Meter | `meter.c` | Signal strength gauge |
| Menus | `*_menu.c` (15+) | Drop-down menus for all functions |
| Dialogs | `action_dialog.c` | Modal windows |

**Display Modules (render-heavy):**
- **Cairo-based drawing:** Panadapter, waterfall, meter backgrounds
- **OpenGL acceleration:** (optional via GTK native)
- **FFT computation:** Sourced from WDSP analyzer channels

**Custom CSS Theming:** `src/css.c`
- Dark/light theme support
- Font customization
- Color palette management

**Appearance System:** `src/appearance.c`
- Skin/theme selection
- Layout persistence
- Window geometry save/restore

---

## 8. Kernmodules - Tabel

| Module | Bestand | Lijnen | Verantwoordelijkheid |
|---|---|---|---|
| Main/Init | `main.c` | ~1500 | App startup, GTK setup, plugin loader |
| Radio State | `radio.c` | ~2000 | Global radio state machine, TX/RX control |
| Receiver | `receiver.c` | ~2500 | Multi-receiver instantiation, WDSP RXA binding |
| Transmitter | `transmitter.c` | ~2500 | TX chain init, WDSP TXA binding, drive control |
| Protocol 2 | `new_protocol.c` | ~3500 | UDP Metis framing, sample exchange, discovery |
| Protocol 1 | `old_protocol.c` | ~3000 | OZY/Metis legacy framing, backward compat |
| Discovery P2 | `new_discovery.c` | ~800 | UDP broadcast discovery for Protocol 2 |
| Discovery P1 | `old_discovery.c` | ~1000 | USB/Network discovery for Protocol 1 |
| VFO | `vfo.c` | ~800 | Frequency management, RIT/XIT, bandstack |
| Mode | `mode.c` | ~600 | Mode enum mapping, filter defaults |
| Filter | `filter.c` | ~500 | Bandpass filter cutoff control |
| CW Engine | `cw_engine.c` | ~1200 | CW tone generation, timing |
| Iambic Keyer | `iambic.c` | ~600 | Iambic firmware emulation |
| Rigctl | `rigctl.c` | ~1500 | Hamlib rigctld server (TCP 4532) |
| TCI Server | `tci.c` | ~2000 | WebSocket TCI protocol, audio stream |
| TCI Audio | `tci_audio.c` | ~600 | TCI PCM audio packaging |
| MIDI (Linux) | `alsa_midi.c` | ~800 | ALSA MIDI CC event binding |
| MIDI (macOS) | `mac_midi.c` | ~800 | CoreMIDI event binding |
| MIDI Menus | `midi_menu.c` | ~1000 | MIDI binding UI |
| PulseAudio | `pulseaudio.c` | ~1000 | PulseAudio sink/source management |
| ALSA Audio | `audio.c` | ~900 | ALSA PCM configuration |
| PortAudio | `portaudio.c` | ~900 | macOS PortAudio I/O |
| Menus (Rx) | `rx_menu.c` | ~1500 | RX filter, mode, AGC menus |
| Menus (Tx) | `tx_menu.c` | ~1500 | TX filter, drive, comp menus |
| Menus (Display) | `display_menu.c` | ~1000 | Panadapter/waterfall options |
| Menus (DSP) | `noise_menu.c` | ~1000 | NR/NB/ANF controls |
| Panadapter RX | `rx_panadapter.c` | ~1500 | RX spectrum drawing (Cairo) |
| Panadapter TX | `tx_panadapter.c` | ~1500 | TX spectrum drawing (Cairo) |
| Waterfall | `waterfall.c` | ~2000 | Waterfall scrolling + color mapping |
| Meter Display | `meter.c` | ~800 | S-meter + power meter rendering |
| Greyline | `greyline.c` | ~1000 | Solar terminator calculation |
| Settings Store | `store.c` | ~2000 | XML/JSON persistence |
| Version Control | `version.c` | ~200 | Build version embedding |

---

## 9. CAT/TCI/MIDI Integraties

### CAT (Computer-Aided Transceiver) - Rigctld Protocol
**Bestand:** `src/rigctl.c`

**Implementation:**
- TCP server on port 4532 (localhost by default)
- Implements Hamlib rigctld protocol commands
- Subset of full Kenwood TS-2000 commands
- Null-terminated ASCII responses

**Example Commands:**
```
get_freq -> "14200000\n"           (Current VFO frequency)
set_freq 7150000 -> "\n"           (Set frequency)
get_mode -> "USB 3000\n"           (Mode + BW)
set_mode USB -> "\n"
get_level STRENGTH -> "-5\n"       (S-meter dBm)
set_ptt 1 -> "\n"                  (PTT ON)
```

**Clients:** WSJT-X, FLDIGI, N1MM Logger, etc.

---

### TCI (Transceiver Control Interface) - WebSocket
**Bestanden:** `src/tci.c`, `src/tci_audio.c`

**Protocol Details:**
- WebSocket über TCP (default port 9100)
- TLS optional (libwebsockets + OpenSSL)
- Binary framing (variable-length messages)
- Audio PCM streaming (48 kHz float32)

**Command Categories:**
1. **Frequency Control:** VFO, RIT/XIT
2. **Mode/Filter:** Mode selection, bandwidth
3. **Receiver Control:** AGC, NR, NB, gain
4. **Transmitter Control:** Drive, TX filter
5. **Meter Telemetry:** S-meter, power, SWR
6. **Audio Stream:** TX mic/RX demod PCM

**Connection Flow:**
```
Client → TCP:9100 → TLS Handshake (optional)
        → WebSocket upgrade request
        → Receive telemetry frames (30 Hz)
        → Send control frames (variable)
        → Audio stream (PCM @ 48 kHz)
```

---

### MIDI (Musical Instrument Digital Interface)

#### Linux (ALSA MIDI)
**Bestand:** `src/alsa_midi.c`

**Features:**
- Control Change (CC) binding to radio parameters
- Fader/knob → frequency/drive/filter mapping
- Button → mode/PTT/tuning
- ALSA sequencer API (libasound)

#### macOS (CoreMIDI)
**Bestand:** `src/mac_midi.c`

**Features:**
- CoreMIDI client registration
- MIDI event polling
- Same CC mapping as Linux

**MIDI CC Mapping (from midi_menu.c):**
| CC# | Function | Range |
|---|---|---|
| 1 | Mic Gain | 0-100 |
| 2 | AF Gain | 0-100 |
| 3 | Drive | 0-100 |
| 7 | Volume | 0-100 |
| 14 | VFO Tune | ±10 kHz |
| 64 | PTT | 0=OFF, 127=ON |
| Custom | Freq Up/Down | ± increments |

---

## 10. Sterke Punten

1. **Robuste Protocol Ondersteuning:** Beide Protocol 1 en Protocol 2 in één codebase
2. **Cross-Platform:** Linux/macOS compilatie met audio abstraction layer
3. **Advanced DSP:** WDSP integration voor professionele noise reduction (NR1/NR2)
4. **Extensibility:** Plugin framework via GTK/GObject introspection
5. **Network Control:** TCI WebSocket + Rigctld voor remote operation
6. **Hardware Range:** Ondersteunt 10+ HPSDR board types
7. **Active Development:** DL1BZ onderhoudend, regelmatige updates
8. **Memory Efficient:** C-code met expliciet geheugen management
9. **Audio Abstraction:** Ondersteunt PulseAudio/ALSA/PortAudio
10. **Compiler Optimizations:** -O3 build, architecture-specific flags (clang, AVX2)

---

## 11. Zwakke Punten

1. **Monolitische C Codebase:** Grote .c bestanden (2000-3500 regels) zonder helder modulair ontwerp
2. **Verouderde UI Framework:** GTK 3 geen moderne design, weinig responsive
3. **Geheugen Safety:** Geen bounds checking in buffer ring implementation (vertrouwt op correcte mutexen)
4. **Testdekkking:** Geen formele unit tests zichtbaar; integratie tests afhankelijk van hardware
5. **Multithreading Complexiteit:** 5+ threads zonder duidelijke synchronisatie primitives (semaforen/mutexen sparsely documented)
6. **MIDI/TCI Debugging:** Tekstprotocollen, moeilijk interactief te debuggen
7. **Platform Dependencies:** Makefile sterk afhankelijk van pkg-config / Homebrew
8. **Docs:** Interne documentatie minderjarig; veel context in comments verloren
9. **Error Handling:** Weinig graceful degradation (bv. audio device failover)
10. **Legacy Code:** Old_protocol.c dodesteile code maar nog nodig voor backward compat

---

## 12. Opmerking: TCI Header

**Bestand:** `src/tci.h` - Niet ontdekt in provided analysis
Waarschijnlijk bevat: WebSocket frame typedef, audio packet structure, command enum
Aanbevolen: Uitvoeriger lezen van tci.c:1-100 voor interface signatures

---

## Conclusie voor NovaSdr

**Bruikbare referentiebronnen uit deskHPSDR:**
1. **Protocol 2 wire format:** Exact DDC/RX/TX framing (50+ MHz bandwidth support)
2. **WDSP integration:** RXA/TXA init ordering, meter pipeline routing
3. **Multi-receiver plumbing:** Ring buffer + thread-safe sample exchange
4. **CW keyer:** Iambic algorithm + timing (if implementing CW TX)
5. **Audio abstraction:** Decoupled I/O backend (PulseAudio/ALSA pattern)
6. **TCI/Rigctld:** Network control patterns (TCP + WebSocket simultaneous)
7. **GTK UI:** Display update synchronization (if building GUI version)

**Vermijden:**
- Rechtstreekse Protocol 1 replicatie (legacy)
- Volledige WDSP statische linking (prefer P/Invoke variant)
- Complexe MIDI feature parity (niche)

