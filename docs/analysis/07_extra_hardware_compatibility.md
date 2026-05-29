# NovaSdr — Fase 7: Extra Hardwarecompatibiliteit
*Evidence-based analyse | Gegenereerd: 2026-05-29*

---

## Overzicht

De doelapp NovaSdr mag niet beperkt blijven tot Brick2 / OpenHPSDR-hardware. Dit document analyseert de architecturale vereisten voor drie extra hardwarefamilies en hoe ze passen in het NovaSdr hardware abstraction model.

---

## 7.1 SDRplay Radio's

### Overzicht

SDRplay biedt een reeks RSP (Radio Spectrum Processor) SDR-ontvangers:

| Model | Frequentiebereik | Max. bandbreedte | Bijzonderheden |
|---|---|---|---|
| RSP1A | 1 kHz – 2 GHz | 10 MHz | Budget model, breed bereik |
| RSP2/RSP2pro | 1 kHz – 2 GHz | 10 MHz | Dual antenna, notch filters |
| RSPdx | 1 kHz – 2 GHz | 10 MHz | Verbeterd HF, AM/FM notch |
| RSPduo | 1 kHz – 2 GHz | 10 MHz | Dual-tuner (diversity RX) |
| RSP1B | 1 kHz – 2 GHz | 10 MHz | Opvolger RSP1A |

**Maximale sample rate:** 10 MHz (voor spectrum monitoring); typisch 250 kHz – 2 MHz voor demodulatie

**Capabilities:**
- Hardware AGC (snel, stabiel voor HF/VHF)
- Hardware step attenuator (0-71 dB in stappen)
- Notch filters: AM/FM hardware notch (AM/FM broadcast interference rejection)
- Bias-T (RSP2/RSPdx)
- Geen TX

### Integratie-opties

#### Optie A: SDRplay API 3.x (aanbevolen voor NovaSdr)

**Status:** Proprietaire SDK, Windows + Linux + macOS + Raspberry Pi. Niet open-source.

```
https://www.sdrplay.com/api/
```

**API-structuur (C API):**
```c
// Initialisatie
sdrplay_api_Open()
sdrplay_api_GetDevices(devices, deviceCount, maxDevices)
sdrplay_api_SelectDevice(device)
sdrplay_api_Init(device, cbFns, cbContext)

// Frequentie/gain instellen
sdrplay_api_Update(device, tuner, reasonForUpdate, extendedBitfield)

// Streaming via callback:
typedef void (*sdrplay_api_StreamCallback_t)(
    short *xi, short *xq,
    sdrplay_api_StreamCbParamsT *params,
    unsigned int numSamples,
    unsigned int reset,
    void *cbContext
);
```

**Voordelen:**
- Beste performance en feature-toegang (hardware AGC, notch, attenuator)
- Lage latency (directe callback-model)
- Officieel ondersteund door SDRplay

**Nadelen:**
- Proprietaire binary SDK — **incompatibel met GPL-kern**
- Vereist aparte licentieovereenkomst
- Windows DLL / Linux .so — runtime linked

**NovaSdr implementatie:** `SdrplaySource : IDeviceSource` als **optionele binary-only plugin** buiten GPL-kern

```csharp
// NovaSdr.Devices.SdrPlay/SdrplaySource.cs
// Vereist: sdrplay_api.dll (Windows) / libsdrplay_api.so (Linux) geïnstalleerd
[SupportedOSPlatform("windows"), SupportedOSPlatform("linux")]
public sealed class SdrplaySource : IDeviceSource
{
    public DeviceCapabilities Capabilities =>
        DeviceCapabilities.Receive |
        DeviceCapabilities.HardwareAtt |
        DeviceCapabilities.HwAGC |
        DeviceCapabilities.BiasTee;

    // sdrplay_api_StreamCallback_t wrapper → IqBlock producer
    // Sample rates: 250kHz, 500kHz, 1MHz, 2MHz, 6MHz, 8MHz, 10MHz
    // IQ decimation naar 48kHz voor WDSP FeedIq()
}
```

#### Optie B: SoapySDR met sdrplay driver

**Status:** Open-source SoapySDR driver voor SDRplay. Minder features dan native API.

```
https://github.com/pothosware/SoapySDRPlay3
```

**Voordelen:**
- Uniforme API via SoapySdrSource — één adapter voor alle SoapySDR-compatibele devices
- GPL-compatibel distributiepad

**Nadelen:**
- Vereist ook SDRplay proprietary API geïnstalleerd (als backend)
- Minder hardware-specifieke features (geen hardware notch via SoapySDR)
- Hogere latency dan native API

**Aanbeveling:** Optie A als primaire pad; Optie B als fallback voor gebruikers zonder SDRplay SDK.

### SDRplay als RX2 in NovaSdr

**Typische latency:** 25-40 ms (SDRplay API internal buffering ~20ms + WDSP 1024@48k = 21ms)

**Beste use cases:**
- HF band monitoring (1-30 MHz) als tweede oog naast Brick2
- Diversity receive op VHF/UHF (RSPduo dual-tuner)
- Breedbandige spectrum awareness (tot 10 MHz zichtbaar)

**Sample rate bridge:** SDRplay levert IQ bij 250kHz–10MHz. WDSP verwacht 48kHz.
- Decimatie via SoapySDR internal filter, of
- libsamplerate decimatie in `SampleRateBridge` klasse

---

## 7.2 RTL-SDR Dongels

### Overzicht

RTL-SDR is een op Realtek RTL2832U gebaseerde goedkope SDR-ontvanger:

| Model | Frequentiebereik | Max. bandbreedte | Bijzonderheden |
|---|---|---|---|
| RTL-SDR V3 | 500 kHz – 1.766 GHz | 3.2 MHz | Direct sampling HF (0.5-28.8 MHz) |
| RTL-SDR Blog V4 | 500 kHz – 1.766 GHz | 3.2 MHz | Verbet. ruis, bias-T |
| Generic RTL2832U | 24 MHz – 1.766 GHz | 3.2 MHz | Geen direct sampling |

**Capabilities:**
- RX only (geen TX)
- Direct sampling mode (RTL-SDR V3/V4): HF zonder upconverter
- Bias-T (RTL-SDR V3/V4)
- Geen hardware AGC (software AGC via WDSP)
- Sterk beïnvloedbaar door harmonischen van oscillator

### Integratie via librtlsdr

**Status:** Open-source, GNU GPL v2. Volledige GPL-kern compatibiliteit.

```
https://github.com/osmocom/rtl-sdr
```

**P/Invoke interface (gesimplificeerd):**
```c
// discovery
int rtlsdr_get_device_count();
char* rtlsdr_get_device_name(uint32_t index);

// open/close
int rtlsdr_open(rtlsdr_dev_t **dev, uint32_t index);
int rtlsdr_close(rtlsdr_dev_t *dev);

// configure
int rtlsdr_set_center_freq(rtlsdr_dev_t *dev, uint32_t freq);
int rtlsdr_set_sample_rate(rtlsdr_dev_t *dev, uint32_t rate);
int rtlsdr_set_tuner_gain(rtlsdr_dev_t *dev, int gain);

// streaming (async callback)
int rtlsdr_read_async(rtlsdr_dev_t *dev, rtlsdr_read_async_cb_t cb,
                       void *ctx, uint32_t buf_num, uint32_t buf_len);
```

**NovaSdr implementatie:**

```csharp
// NovaSdr.Devices.RtlSdr/RtlSdrSource.cs (GPL v2+)
public sealed class RtlSdrSource : IDeviceSource
{
    public DeviceCapabilities Capabilities =>
        DeviceCapabilities.Receive |
        DeviceCapabilities.DirectSample |  // V3/V4 only (detecteer via device string)
        DeviceCapabilities.BiasTee;        // V3/V4 only

    // rtlsdr_read_async callback → IqBlock producer
    // Supported sample rates: 225001-300000 Hz, 900001-3200000 Hz
    // Typisch: 2048000 Hz (2.048 MSps)
    // Bridge: 2.048 MHz → 48 kHz via SampleRateBridge (decimatie factor 42.67)
}
```

**Direct sampling mode (HF):**
```csharp
// RTL-SDR V3: direct sampling via Q branch (0-14.4 MHz) of I branch (14.4-28.8 MHz)
await SetDirectSamplingModeAsync(DirectSamplingMode.Q); // < 14.4 MHz
await SetDirectSamplingModeAsync(DirectSamplingMode.I); // 14.4-28.8 MHz
```

### RTL-SDR als RX2 in NovaSdr

**Typische latency:** 50-100 ms (librtlsdr buffer grootte standaard = 8×16384 bytes = ~130K samples @ 2.048 MHz ≈ 64ms)

**Beste use cases:**
- Goedkope panadapter/monitor receiver
- HF band monitoring naast Brick2
- Scanner (VHF/UHF monitoring)
- Ontwikkelplatform en test voor multi-device code

**Beperkingen:**
- Frequentiestabiliteit beperkt (kristal oscillator drift, ±1-5 ppm uncalibrated)
- Hogere ruis dan SDRplay of Brick2
- Geen hardware AGC
- Beperkte dynamic range (~48 dB)

**Aanbeveling:** RTL-SDR als **primaire testplatform** voor NovaSdr multi-device development. Goedkoop, breed beschikbaar, volledig GPL-compatibel.

---

## 7.3 PlutoSDR en PlutoPlus

### Overzicht

PlutoSDR is een ADALM-PLUTO SDR-apparaat van Analog Devices gebaseerd op AD9363/AD9364 transceiver chip:

| Model | Frequentiebereik | Max. bandbreedte | TX | Bijzonderheden |
|---|---|---|---|---|
| ADALM-PLUTO (origineel) | 325 MHz – 3.8 GHz | 20 MHz | Ja | AD9363, 12-bit ADC |
| PlutoPlus (modified fw) | 70 MHz – 6 GHz | 56 MHz | Ja | AD9364, uitgebreid bereik via custom firmware |

**PlutoPlus** is geen officieel Analog Devices product — het is een custom firmware patch die het frequentiebereik en de bandbreedte van de AD9364 chip ontgrendelt. Wijdverspreid in de amateur-radio gemeenschap.

**Capabilities:**
- Full-duplex TX + RX simultaan
- Breed frequentiebereik (PlutoPlus: 70 MHz–6 GHz)
- 12-bit ADC/DAC
- Sample rates tot 61.44 MSPS
- Ethernet of USB verbinding
- libiio open-source driver (LGPL)

### Integratie via libiio

**Status:** Open-source, LGPL v2.1. Compatibel met GPL als dynamisch gelinkt.

```
https://github.com/analogdevicesinc/libiio
```

**libiio concepten:**
- `iio_context` — verbinding met het apparaat (netwerk of USB)
- `iio_device` — logisch apparaat (bijv. "cf-ad9361-lpc")
- `iio_channel` — RX of TX kanaal
- `iio_buffer` — DMA buffer voor IQ samples

**P/Invoke interface (gesimplificeerd):**
```c
// Context aanmaken (Ethernet)
struct iio_context* iio_create_network_context(const char *host);

// Device vinden
struct iio_device* iio_context_find_device(struct iio_context *ctx, const char *name);

// Kanaal configureren
struct iio_channel* iio_device_find_channel(struct iio_device *dev, const char *name, bool output);
iio_channel_enable(channel);

// Attributen instellen
iio_channel_attr_write_longlong(channel, "sampling_frequency", 2500000);  // 2.5 MHz
iio_channel_attr_write_longlong(channel, "rf_bandwidth", 2000000);
iio_channel_attr_write_double(channel, "hardwaregain", -10.0);

// Buffer aanmaken en lezen
struct iio_buffer* iio_device_create_buffer(dev, samples_count, cyclic);
ssize_t iio_buffer_refill(buffer);
void* iio_buffer_first(buffer, channel);
```

**NovaSdr implementatie:**

```csharp
// NovaSdr.Devices.PlutoSdr/PlutoSdrSource.cs (LGPL-compatible)
public sealed class PlutoSdrSource : IDeviceSource
{
    public DeviceCapabilities Capabilities =>
        DeviceCapabilities.Receive |
        DeviceCapabilities.FullDuplex | // Als ITransceiver
        DeviceCapabilities.WideFreq;    // PlutoPlus: 70MHz-6GHz

    // libiio via P/Invoke
    // iio_create_network_context("192.168.2.1")  // PlutoSDR default IP
    // IQ stream via iio_buffer_refill() → IqBlock
    // Sample rate bridge: 2.5 MHz → 48 kHz voor WDSP
}

// Als volledige transceiver:
public sealed class PlutoSdrTransceiver : ITransceiver
{
    // TX path: WDSP TXA output → IQ buffer → iio_buffer_push()
    // RX path: iio_buffer_refill() → SampleRateBridge → WdspDspEngine.FeedIq()
    // Full-duplex: gelijktijdig RX + TX
}
```

### PlutoSDR/PlutoPlus als primaire backend

**PlutoPlus als zelfstandige transceiver in NovaSdr:**
- Frequentiebereik 70 MHz – 6 GHz past bij VHF/UHF/microwave experimenten
- Geen HF ondersteuning (< 70 MHz) — gebruik Brick2 voor HF
- Full-duplex: ideaal voor duplex QSO's op UHF (bijv. satellieten, linken)
- TX power: ~0 dBm (extern PA vereist voor QRV)
- Latency via Ethernet: ~25-30 ms (geschat)

**PlutoSDR als RX2 voor PureSignal feedback (toekomst):**
- PlutoPlus kan TX-output van Brick2 samplen als PS feedback-pad
- Vereist frequentiegelijkschakeling en timing-compensatie
- Fase 3 roadmap item

---

## 7.4 Hardware Capability Matrix

```
[Flags]
public enum DeviceCapabilities
{
    None            = 0,
    Receive         = 1 << 0,   // RX mogelijk
    Transmit        = 1 << 1,   // TX mogelijk
    FullDuplex      = 1 << 2,   // Simultaan RX+TX
    DualRx          = 1 << 3,   // Twee onafhankelijke DDC paden
    PureSignal      = 1 << 4,   // PS 2.0 loopback feedback
    VariableRate    = 1 << 5,   // Sample rate switchbaar
    HardwareAtt     = 1 << 6,   // Stepped hardware attenuator
    DiversityRx     = 1 << 7,   // Phase-coherente diversity ontvangst
    HwAGC           = 1 << 8,   // Hardware AGC (SDRplay RSP)
    BiasTee         = 1 << 9,   // Bias-T voeding op antenne-poort
    DirectSample    = 1 << 10,  // HF direct sampling (RTL-SDR V3+)
    WideFreq        = 1 << 11,  // > 30 MHz tot GHz bereik (PlutoPlus)
}
```

### Per-device capabilities

| Device | Receive | Transmit | FullDuplex | HwAGC | BiasTee | DirectSample | WideFreq | DualRx | PureSignal |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Brick2 (P1)** | ✓ | ✓ | ✓ | — | — | — | — | ✓ | ✓ |
| **Brick2 (P2)** | ✓ | ✓ | ✓ | — | — | — | — | ✓ | ✓ |
| **Hermes-Lite 2** | ✓ | ✓ | ✓ | — | — | — | — | ✓ | beperkt |
| **ANAN G2 MkII** | ✓ | ✓ | ✓ | — | — | — | — | ✓ | ✓ |
| **Saturn G2** | ✓ | ✓ | ✓ | — | — | — | — | ✓ | ✓ |
| **SDRplay RSP1A** | ✓ | — | — | ✓ | — | — | — | — | — |
| **SDRplay RSPdx** | ✓ | — | — | ✓ | ✓ | — | — | — | — |
| **SDRplay RSPduo** | ✓ | — | — | ✓ | — | — | — | ✓ | — |
| **RTL-SDR V3** | ✓ | — | — | — | ✓ | ✓ | — | — | — |
| **RTL-SDR V4** | ✓ | — | — | — | ✓ | ✓ | — | — | — |
| **PlutoSDR** | ✓ | ✓ | ✓ | — | — | — | — | — | — |
| **PlutoPlus** | ✓ | ✓ | ✓ | — | — | — | ✓ | — | — |
| **STEMlab (via HTTP)** | ✓ | ✓ | ✓ | — | — | — | — | ✓ | — |

---

## 7.5 DeviceRegistry en Discovery

### Unified discovery flow

```
DiscoveryAggregatorService.DiscoverAllAsync()
├── OpenHpsdrDiscovery.DiscoverAsync()       // UDP broadcast 1024
│    └── Returns: List<DiscoveredRadio>      // P1 + P2 devices
│
├── SoapySdrEnumerator.EnumerateAsync()      // soapy::Device::enumerate()
│    └── Returns: List<SoapyDeviceInfo>      // RTL-SDR, SDRplay (via driver)
│
├── SdrplayNativeEnumerator.EnumerateAsync() // sdrplay_api_GetDevices()
│    └── Returns: List<SdrplayDeviceInfo>    // SDRplay direct (als SDK aanwezig)
│
└── IioScanner.ScanAsync()                  // iio_create_network_context scan
     └── Returns: List<IioDeviceInfo>        // PlutoSDR/PlutoPlus op netwerk
```

### DeviceRegistry

```csharp
public interface IDeviceRegistry
{
    IReadOnlyList<IDeviceDescriptor> DiscoveredDevices { get; }
    event EventHandler<DeviceDiscoveredEventArgs> DeviceDiscovered;
    event EventHandler<DeviceRemovedEventArgs> DeviceRemoved;

    Task<IDeviceSource> CreateSourceAsync(string deviceId, CancellationToken ct);
    Task<ITransceiver> CreateTransceiverAsync(string deviceId, CancellationToken ct);
}
```

---

## 7.6 GPL Licentie Overwegingen

| Device driver | Licentie | Distributie in GPL-kern? |
|---|---|---|
| librtlsdr | GPL v2 | ✓ Ja, volledig |
| libiio (PlutoSDR) | LGPL v2.1 | ✓ Ja, als dynamisch gelinkt |
| SoapySDR | BSL-1.0 (permissive) | ✓ Ja |
| SoapySDRPlay3 driver | MIT | ✓ Ja |
| SDRplay API 3.x | Proprietary | ✗ Nee — als optioneel binary plugin |
| SDRplay API (geïnstalleerd door gebruiker) | Proprietary (gratis) | ✓ Runtime linked (LGPL-model) |

**Distributiestrategie:**
- `NovaSdr.Devices.RtlSdr` — onderdeel van GPL-kern
- `NovaSdr.Devices.PlutoSdr` — onderdeel van GPL-kern (LGPL libiio dynamisch)
- `NovaSdr.Devices.SoapySdr` — onderdeel van GPL-kern
- `NovaSdr.Devices.SdrPlay` — **optionele plugin**, niet in standaard distributie
  - Gebruiker installeert SDRplay API 3.x zelf
  - NovaSdr laadt plugin dynamisch via `IZeusPlugin` als API gevonden wordt

---

## 7.7 Samenvatting Aanbevelingen

1. **Gebruik `IDeviceSource`/`ITransceiver` als centrale abstractie** — elk device implementeert exact dezelfde interface, ongeacht protocol
2. **RTL-SDR als MVP RX2 target** — goedkoop, GPL-compatibel, breed beschikbaar
3. **SDRplay via SoapySDR als fase 2** — brede hardware-dekking zonder proprietary code in kern
4. **SDRplay native API als optionele plugin** — voor gebruikers die maximale performance willen
5. **PlutoSDR als fase 2 transceiver-optie** — unieke waarde voor VHF/UHF/micro-wave operators
6. **Capability model per device registreren** — UI adapteert automatisch (verberg TX controls als `!capabilities.HasFlag(Transmit)`)
7. **SampleRateBridge als centrale klasse** — alle devices leveren IQ → 48kHz voor WDSP, ongeacht native sample rate
