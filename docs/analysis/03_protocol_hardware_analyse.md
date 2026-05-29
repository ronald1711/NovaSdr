# 03 — Protocol & Hardware Analyse

> Vastgestelde bronfeiten. Geen aannames buiten de gedocumenteerde implementaties.
> Doel: volledige protocol- en hardware-analyse als basis voor NovaSdr's abstracte adapterlaag.

---

## Inhoudsopgave

1. [Protocol 1 — implementatie per project](#1-protocol-1--implementatie-per-project)
2. [Protocol 2 — implementatie per project](#2-protocol-2--implementatie-per-project)
3. [Brick2 / Hermes-Lite 2 specifieke details](#3-brick2--hermes-lite-2-specifieke-details)
4. [HpsdrBoardKind enumeratie](#4-hpsdrboardkind-enumeratie)
5. [Vergelijkingstabel P1 vs P2 feature-pariteit](#5-vergelijkingstabel-p1-vs-p2-feature-pariteit)
6. [Abstracte protocol adapterlaag (interface code)](#6-abstracte-protocol-adapterlaag-interface-code)
7. [Backward compatibility strategie](#7-backward-compatibility-strategie)
8. [Risico's en onbevestigde aannames](#8-risicos-en-onbevestigde-aannames)
9. [Capability-based device model ontwerp](#9-capability-based-device-model-ontwerp)

---

## 1. Protocol 1 — Implementatie per Project

### 1.1 Overzicht Protocol 1 (HPSDR Legacy Protocol)

Protocol 1 is het originele HPSDR-protocol, ontwikkeld rond 2006–2010. Het gebruikt UDP op poort 1024 met vaste 512-byte frames en een simplistische control-frame structuur.

**Kernkenmerken:**
- UDP poort 1024 (bidirectioneel)
- 512-byte frames (vaste grootte)
- SYNC patroon: 3× 0x7F bytes aan begin van elk frame
- C&C (Command and Control) bytes in elk frame
- Interleaved IQ samples en microfoon data
- Discovery via UDP broadcast

---

### 1.2 Protocol 1 — deskHPSDR (old_protocol.c, 3396 regels)

**Discovery:**
```
Client broadcast → 255.255.255.255:1024
  Payload: [0xEF, 0xFE, 0x02, 0x00 × 60] (63 bytes)

Hardware response → unicast naar client:
  [0xEF, 0xFE, 0x02 of 0x03,  ← 0x02=not-in-use, 0x03=in-use
   MAC[0..5],
   board_code,
   version,
   0x00 × padding]  (63 bytes)
```

**Frame structuur (Hardware → PC, RX data):**
```
Offset  Grootte  Inhoud
0       3        SYNC = 0x7F 0x7F 0x7F
3       1        C&C byte 0 (PTT status, dot, dash, drive, etc.)
4       1        C&C byte 1 (receiver 0 frequency, bits 31-24)
5       1        C&C byte 2 (receiver 0 frequency, bits 23-16)
6       1        C&C byte 3 (receiver 0 frequency, bits 15-8)
7       1        C&C byte 4 (receiver 0 frequency, bits 7-0)
8–511   504      IQ samples + mic data
                 IQ: 24-bit I + 24-bit Q per sample
                 Mic: 16-bit PCM stereo
```

**Frame structuur (PC → Hardware, TX data):**
```
Offset  Grootte  Inhoud
0       3        SYNC = 0x7F 0x7F 0x7F
3       1        C&C byte 0 (bits: MOX, speed, 10MHz ref, etc.)
4-7     4        C&C bytes 1-4 (TX frequency, LO frequency, etc.)
8-511   504      TX IQ samples (24-bit) + audio feedback
```

**Transport/timing:**
- Frame rate: hardware-driven (hardware stuurt RX frames op vaste interval)
- TX: PC stuurt frames op 48kHz sample clock timing (~512 samples/frame = ~10.67ms/frame)
- Geen expliciete flow control; UDP best-effort
- Geen acknowledgment; packet loss = samples verloren

**deskHPSDR specifieke implementatie details:**
- `receive_thread()`: blocking `recvfrom()`, frame validatie via SYNC check, C&C parsing
- Meerdere ontvangers mogelijk via meerdere C&C cycles per UDP frame
- `send_thread()`: frame constructie + `sendto()`, timing via `usleep()` of semaphore
- Hardware state machine: IDLE → DISCOVERED → RUNNING → STOPPED
- Foutafhandeling: `perror()` + counter increment, geen recovery

---

### 1.3 Protocol 1 — Zeus (Protocol1Client.cs)

**Discovery:**
```csharp
// Parallel UDP broadcast op beide protocollen
// Zeus.Protocol1/Protocol1Client.cs
await udpClient.SendAsync(discoveryPacket, endpoint);
// Response parsing → hardware info struct
```

**Control frame structuur (Zeus definitie):**
- **1032-byte control frames** (afwijkend van de 512-byte standaard P1 data frames)
- Dit suggereert dat Zeus een gecombineerde control + data frame formaat gebruikt, of dat de 1032-byte betrekking heeft op een specifieke operating mode.

**Semaphore-driven TX pacing:**
```csharp
// TX timing via SemaphoreSlim
await _txSemaphore.WaitAsync(cancellationToken);
// Bouw TX frame
await _udpClient.SendAsync(txFrame, _hardwareEndpoint);
// Semaphore wordt gereleased door timer callback op sample-clock interval
```

**Verschil met deskHPSDR:**
| Aspect | deskHPSDR | Zeus |
|--------|-----------|------|
| Frame grootte | 512 bytes (data) | 1032 bytes (control) |
| TX timing | usleep/semaphore | SemaphoreSlim async |
| Discovery | Sync broadcast | Parallel async broadcast |
| Error handling | perror() + exit | Exception handling + logging |
| Threading | pthreads | async/await + Task |

---

### 1.4 Protocol 1 — Thetis (NetworkIO.cs, 1400 regels)

- `clsRadioDiscovery.cs` (1500 regels) implementeert P1/P2 auto-detect
- Klassieke synchrone UDP socket receive in background Thread
- `HpsdrBoardKind` enum voor board-type detectie op basis van discovery response
- Geen abstractielaag; direct gekoppeld aan WinForms UI state

---

## 2. Protocol 2 — Implementatie per Project

### 2.1 Overzicht Protocol 2 (HPSDR New Protocol)

Protocol 2 is de modernere opvolger, ontworpen voor hogere sample rates, meerdere ontvangers (DDC = Digital Down Converters), en betere TCP-gebaseerde control.

**Kernkenmerken:**
- UDP poorten 1024–1042 (data poorten, één per DDC/DUC)
- 1500-byte UDP frames (MTU-optimaal voor Ethernet)
- TCP poort 1024 voor discovery en high-level control
- MAX_DDC = 4 (maximaal 4 onafhankelijke ontvangers)
- Hogere sample rates: tot 384 kHz (DDC decimation configureerbaar)
- Verbeterde timing via hardware timestamps
- Phase words voor frequentie-instelling (32-bit fractional NCO)

---

### 2.2 Protocol 2 — deskHPSDR (new_protocol.c, 2759 regels)

**DDC Routing:**
```
Hardware NCO (DDC 0..3)
  │ 24-bit IQ @ configureerbare rate (48/96/192/384 kHz)
  ▼
UDP poort 1024+DDC_index
  ▼
new_protocol.c receive_thread()
  │ per DDC: separate RXA channel in WDSP
  ▼
WDSP RXA[0..3] parallel processing
```

**Frame structuur (Hardware → PC, P2 data):**
```
Offset  Grootte  Inhoud
0       2        Sequence number (big-endian)
2       1        End-point code (0x84 = DDC data)
3       1        DDC index (0-3)
4       4        Timestamp (nanoseconds, hardware clock)
8       4        Bits per sample (24)
12      4        Sample rate
16      4        Number of samples in this frame
20      N×6     IQ samples: 24-bit I (MSB-first) + 24-bit Q (MSB-first)
...             Remainder: 0-padded to 1500 bytes
```

**Phase word voor frequentie:**
```
Frequentie instelling via 32-bit phase word:
  phase_word = (uint32_t)(frequency × 2^32 / sample_clock)

Verzonden via control frame (TCP of UDP control port):
  [DDC_index][phase_word_32bit][decimation_factor]
```

**TX DUC (Digital Up Converter):**
```
PC → Hardware DUC:
  UDP poort 1025 (DUC data)
  Frame: sequence + timestamp + IQ samples @ 192kHz (P2 max)
  Hardware interpolates to DAC sample rate (typically 122.88 MHz)
```

**deskHPSDR implementation notes:**
- MAX_DDC=4 geconfigureerd in new_protocol.c
- Elke DDC heeft eigen UDP socket binding
- Thread per DDC socket (of gemeenschappelijke receive thread met demux)
- WDSP RXA channels 0..3 parallel gecreëerd

---

### 2.3 Protocol 2 — Zeus (Protocol2Client.cs)

**Frame structuur (Zeus definitie):**
- **1444-byte frames** (iets kleiner dan de 1500-byte MTU standaard — wellicht overhead voor encapsulatie of specifieke hardware variant)

**DDC routing in Zeus:**
```csharp
// Zeus.Protocol2/Protocol2Client.cs
private readonly Dictionary<int, RxIqBuffer> _ddcBuffers;

private void ProcessFrame(ReadOnlySpan<byte> frame)
{
    var ddcIndex = frame[3];
    var buffer = _ddcBuffers[ddcIndex];
    buffer.Write(frame.Slice(20)); // IQ samples
    // → IDspEngine.SetRxInput(ddcIndex, iqSpan)
}
```

**TX buffer sizing (CFIR upsampling chain):**
```
TX DSP pipeline (Protocol 2 specificatie):
  Input:  512 samples @ 48 kHz   (audio in)
  Stage1: 1024 samples @ 96 kHz  (2× upsample via CFIR)
  Stage2: 2048 samples @ 192 kHz (2× upsample via CFIR)
  Output: 1444-byte P2 DUC frame @ 192 kHz

CFIR = Cascaded FIR (Warren Pratt's approach voor kwaliteits-upsampling)
```

**Semaphore-driven TX pacing (Zeus):**
```csharp
// Timer-based TX release op 192kHz-afgeleid interval
// ~2048 samples / 192000 Hz = ~10.67 ms per frame
private readonly PeriodicTimer _txTimer = new(TimeSpan.FromMilliseconds(10.67));
```

---

### 2.4 Protocol 2 — Thetis

- `clsRadioDiscovery.cs` detecteert P2 hardware automatisch
- `NetworkIO.cs` bevat P2 UDP socket code naast P1
- Geen expliciete CFIR documentatie in beschikbare feiten
- DDC routing vermoedelijk aanwezig maar niet gedetailleerd gedocumenteerd

---

## 3. Brick2 / Hermes-Lite 2 Specifieke Details

### 3.1 Hermes-Lite 2 (HL2)

De Hermes-Lite 2 is een low-cost open-source SDR transceiver die het HPSDR Protocol 2 implementeert met enkele uitbreidingen.

**Board identificatie:**
- Board code in P1 discovery response: `0x06` (Hermes-Lite)
- HL2 specifiek: extended board code of versie byte in discovery response
- `HpsdrBoardKind.HermesLite` in Thetis enum

**HL2 specifieke eigenschappen:**
- **ADC:** 76.8 MHz clock (afwijkend van standaard 122.88 MHz Hermes)
- **TX power:** 5W max (geen PA)
- **Protocol 2 met HL2 extensions:**
  - Uitgebreide command bytes voor ATU controle
  - I2C bus access via protocol extensie (voor externe LPF/BPF)
  - Extended board info bytes in discovery response
- **EEPROM registers:** Board-type, firmware versie, MAC adres

**HL2 Discovery response (uitgebreid):**
```
Byte 0-1:  0xEF 0xFE
Byte 2:    Status (0x02=available, 0x03=busy)
Byte 3-8:  MAC address
Byte 9:    Board code (0x06 voor HL2)
Byte 10:   Firmware major version
Byte 11:   Firmware minor version
Byte 12:   HL2 board revision
Byte 13:   Features bitmask (ATU, PA, etc.)
```

**HL2 specific protocol extension (gig.protocol.md reference):**
```
Extended C&C bytes voor HL2:
  C&C address 0x17: I2C command (voor externe filters)
  C&C address 0x18: ATU control
  C&C address 0x1F: HL2-specific hardware control
```

### 3.2 Saturn / G2

- Board code: Saturn-specifiek (zie HpsdrBoardKind.Saturn)
- deskHPSDR heeft compile-time `SATURN` flag en `saturn.c` module
- G2 (GenesisRadio) gebruikt extended P2 met eigen registers

---

## 4. HpsdrBoardKind Enumeratie

De volgende waarden zijn gedocumenteerd in Thetis (`HpsdrBoardKind` enum, clsRadioDiscovery.cs):

| Enum waarde | Hex code | Hardware beschrijving |
|-------------|----------|----------------------|
| Atlas | 0x00 | Originele HPSDR Atlas backplane |
| Hermes | 0x01 | Hermes (standalone, 100W) |
| HermesII | 0x02 | Hermes II (verbeterde ADC) |
| Angelia | 0x03 | Angelia (dual ADC, 2× RX) |
| Orion | 0x04 | Orion (ANAN-100D klasse) |
| OrionMKII | 0x05 | Orion MK II (ANAN-7000DLE/8000DLE) |
| HermesLite | 0x06 | Hermes-Lite 1 & 2 |
| Saturn | 0x07 | Saturn / G2 (FPGA-based) |
| *(overige)* | 0x08+ | Toekomstige / third-party boards |

> **Opmerking:** De exacte hex codes voor niet-Thetis boards zijn gebaseerd op algemeen bekende HPSDR specificaties. Thetis gebruikt deze waarden voor auto-detect routing in `clsRadioDiscovery.cs`.

**deskHPSDR compile-time board flags:**
```c
// Compile-time opties voor specifieke hardware:
-DSATURN      // Saturn/G2 specifieke code paden
-DSTEMLAB     // Red Pitaya / STEMlab support
-DAUTOGAIN    // Automatische gain voor specifieke boards
```

---

## 5. Vergelijkingstabel P1 vs P2 Feature-pariteit per Project

| Feature | P1 spec | deskHPSDR P1 | Zeus P1 | Thetis P1 | P2 spec | deskHPSDR P2 | Zeus P2 | Thetis P2 |
|---------|---------|-------------|---------|-----------|---------|-------------|---------|-----------|
| Discovery UDP broadcast | Ja | Ja | Ja | Ja | Ja (TCP) | Ja | Ja | Ja |
| Discovery parallel P1+P2 | n/a | Nee | Ja | Ja | n/a | n/a | Ja | Ja |
| Frame grootte | 512 bytes | 512 | 1032 ctrl | 512 | 1500 bytes | 1500 | 1444 | 1500 |
| SYNC bytes (0x7F×3) | Ja | Ja | n/a (.NET) | Ja | n/a | n/a | n/a | n/a |
| Max DDC (ontvangers) | 1 typisch | 1–8 | ≤4 | ≤8 | 4 | 4 | 4 | 4 |
| Sample rate | 48/96/192kHz | Ja | Ja | Ja | 48–384kHz | Ja | Ja | Ja |
| Phase words (32-bit NCO) | Nee | Nee | Nee | Nee | Ja | Ja | Ja | Ja |
| Hardware timestamps | Nee | Nee | Nee | Nee | Ja | Ja | Ja | Onbekend |
| TX DUC @ 192kHz | Nee | Nee | Nee | Nee | Ja | Ja | Ja | Onbekend |
| CFIR TX upsampling | Nee | Nee | Nee | Nee | Ja | Via WDSP | Ja (TXA) | Via WDSP |
| CAT/rigctld integratie | Extern | Ja | Gepland | Ja | Extern | Ja | Via TCI | Ja |
| TCI server | Nee | Ja (tci.c) | Ja (3357L) | Ja (1000L) | Nee | Ja | Ja | Ja |
| MIDI control | Nee | Optioneel | Gepland | Via Midi2Cat | Nee | Optioneel | Via plugin | Via Midi2Cat |
| N1MM UDP spectrum | Nee | Nee | Onbekend | Ja (1500L) | Nee | Nee | Onbekend | Ja |
| DX cluster TCP | Nee | Ja | Via plugin | Ja | Nee | Ja | Via plugin | Ja |
| HL2 extensions | Nee | Beperkt | Onbekend | Ja | Nee | Beperkt | Onbekend | Ja |
| Saturn/G2 support | Nee | Ja (SATURN) | Onbekend | Ja | Nee | Ja | Onbekend | Ja |

---

## 6. Abstracte Protocol Adapterlaag (Interface Code)

De volgende interface definities zijn ontworpen voor NovaSdr. Ze zijn gebaseerd op de geleerde lessen uit alle drie projecten.

### 6.1 Core Interfaces

```csharp
// NovaSdr.Hardware.Abstractions

/// <summary>
/// Beschrijft een ontdekte HPSDR-compatibele hardware unit.
/// </summary>
public record HardwareDeviceInfo(
    string MacAddress,
    HpsdrBoardKind BoardKind,
    int FirmwareMajor,
    int FirmwareMinor,
    IPEndPoint Endpoint,
    DeviceCapabilities Capabilities
);

/// <summary>
/// Capability-based hardware feature flags.
/// Voorkomt if/else op BoardKind door heel de codebase.
/// </summary>
[Flags]
public enum DeviceCapabilities : ulong
{
    None             = 0,
    Protocol1        = 1 << 0,
    Protocol2        = 1 << 1,
    DualAdc          = 1 << 2,
    FullDuplex       = 1 << 3,
    InternalAtu      = 1 << 4,
    ExternalI2C      = 1 << 5,   // HL2 I2C bus access
    Rx2Stream        = 1 << 6,   // Tweede ontvanger stream
    SaturnExtensions = 1 << 7,
    HermesLiteExt    = 1 << 8,
    HardwareTimestamp = 1 << 9,
    MaxDdc4          = 1 << 10,
    SampleRate384k   = 1 << 11,
}

/// <summary>
/// Abstracte hardware adapter — één implementatie per protocol versie.
/// </summary>
public interface IHardwareAdapter : IAsyncDisposable
{
    HardwareDeviceInfo DeviceInfo { get; }
    HardwareProtocol Protocol { get; }
    HardwareAdapterState State { get; }

    /// <summary>Start hardware en begin IQ streaming.</summary>
    Task StartAsync(HardwareConfiguration config, CancellationToken ct = default);

    /// <summary>Stop hardware en sluit UDP sockets.</summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// RX IQ data stream — meerdere DDC channels mogelijk.
    /// </summary>
    IAsyncEnumerable<RxIqFrame> ReceiveAsync(CancellationToken ct = default);

    /// <summary>Stuur TX IQ frame naar hardware.</summary>
    ValueTask TransmitAsync(ReadOnlyMemory<float> iqSamples, CancellationToken ct = default);

    /// <summary>Configureer een DDC (frequentie, sample rate, decimation).</summary>
    ValueTask ConfigureDdcAsync(int ddcIndex, DdcConfiguration config, CancellationToken ct = default);

    /// <summary>Stel TX frequentie in (P1: C&C bytes, P2: phase word).</summary>
    ValueTask SetTxFrequencyAsync(double frequencyHz, CancellationToken ct = default);

    /// <summary>Hardware-specifieke commando's (HL2 I2C, Saturn registers, etc.).</summary>
    ValueTask SendExtensionCommandAsync(ExtensionCommand command, CancellationToken ct = default);

    /// <summary>Hardware health/status events.</summary>
    IObservable<HardwareStatusEvent> StatusEvents { get; }
}

public enum HardwareProtocol { Protocol1, Protocol2 }

public enum HardwareAdapterState { Disconnected, Discovered, Running, Error }
```

### 6.2 Discovery Service

```csharp
/// <summary>
/// Hardware discovery — parallel P1 en P2 broadcast.
/// Gebaseerd op Zeus parallel discovery pattern.
/// </summary>
public interface IHardwareDiscoveryService
{
    /// <summary>
    /// Zoek HPSDR hardware op het lokale netwerk.
    /// Verstuurt P1 en P2 discovery broadcasts parallel.
    /// </summary>
    IAsyncEnumerable<HardwareDeviceInfo> DiscoverAsync(
        TimeSpan timeout,
        CancellationToken ct = default);

    /// <summary>
    /// Maak de juiste IHardwareAdapter aan op basis van device capabilities.
    /// Factory pattern — caller hoeft protocol niet te kennen.
    /// </summary>
    IHardwareAdapter CreateAdapter(HardwareDeviceInfo device);
}
```

### 6.3 Frame Typen

```csharp
/// <summary>
/// RX IQ frame van hardware (protocol-agnostisch).
/// </summary>
public readonly record struct RxIqFrame(
    int DdcIndex,           // 0-3 voor P2; altijd 0 voor P1
    long TimestampNs,       // Hardware timestamp (P2); 0 voor P1
    int SampleRateHz,       // Actual sample rate
    ReadOnlyMemory<float> IqSamples  // Interleaved I/Q float32
);

/// <summary>
/// DDC configuratie.
/// </summary>
public record DdcConfiguration(
    double FrequencyHz,
    int SampleRateHz,       // 48000 / 96000 / 192000 / 384000
    int DecimationFactor    // Hardware decimation (P2 only)
);

/// <summary>
/// Hardware-specifieke extensie commando's.
/// Bevat board-specifieke payload (HL2 I2C, Saturn registers, etc.)
/// </summary>
public record ExtensionCommand(
    ExtensionCommandType Type,
    ReadOnlyMemory<byte> Payload
);

public enum ExtensionCommandType
{
    HL2_I2C_Write,
    HL2_ATU_Control,
    Saturn_Register_Write,
    Generic_CC_Override
}
```

### 6.4 Protocol 1 Concrete Adapter (schets)

```csharp
/// <summary>
/// Protocol 1 adapter — gebaseerd op deskHPSDR old_protocol.c referentie.
/// </summary>
public sealed class Protocol1Adapter : IHardwareAdapter
{
    // UDP poort 1024, 512-byte frames, SYNC=0x7F×3
    private const int UdpPort = 1024;
    private const int FrameSize = 512;
    private static readonly byte[] SyncBytes = [0x7F, 0x7F, 0x7F];

    private readonly UdpClient _udpClient;
    private readonly Channel<RxIqFrame> _rxChannel;

    public HardwareProtocol Protocol => HardwareProtocol.Protocol1;

    public async IAsyncEnumerable<RxIqFrame> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var frame in _rxChannel.Reader.ReadAllAsync(ct))
            yield return frame;
    }

    private void ParseFrame(ReadOnlySpan<byte> raw)
    {
        // Validate SYNC
        if (raw[0] != 0x7F || raw[1] != 0x7F || raw[2] != 0x7F)
            return;

        // Parse C&C bytes (offset 3-7)
        var cc0 = raw[3]; // PTT, dot, dash, speed
        // ...

        // Parse IQ samples (offset 8-511)
        // 24-bit signed I + 24-bit signed Q per sample
        var samples = new float[63 * 2]; // 63 IQ pairs per frame @ 48kHz
        for (int i = 0; i < 63; i++)
        {
            int offset = 8 + i * 6;
            int iVal = (raw[offset] << 16) | (raw[offset+1] << 8) | raw[offset+2];
            int qVal = (raw[offset+3] << 16) | (raw[offset+4] << 8) | raw[offset+5];
            // Sign extend 24-bit
            if ((iVal & 0x800000) != 0) iVal |= unchecked((int)0xFF000000);
            if ((qVal & 0x800000) != 0) qVal |= unchecked((int)0xFF000000);
            samples[i * 2]     = iVal / 8388608f; // 2^23
            samples[i * 2 + 1] = qVal / 8388608f;
        }

        _rxChannel.Writer.TryWrite(new RxIqFrame(
            DdcIndex: 0,
            TimestampNs: 0,
            SampleRateHz: 48000,
            IqSamples: samples
        ));
    }
    // ... rest van implementatie
}
```

### 6.5 Protocol 2 Concrete Adapter (schets)

```csharp
/// <summary>
/// Protocol 2 adapter — gebaseerd op Zeus Protocol2Client + deskHPSDR new_protocol.c.
/// </summary>
public sealed class Protocol2Adapter : IHardwareAdapter
{
    // Poorten 1024-1042 (basis + DDC index)
    private const int BasePort = 1024;
    private const int MaxDdc = 4;
    private const int FrameSize = 1500; // MTU-optimaal

    private readonly Dictionary<int, UdpClient> _ddcSockets;
    private readonly Channel<RxIqFrame> _rxChannel;

    public HardwareProtocol Protocol => HardwareProtocol.Protocol2;

    public async ValueTask ConfigureDdcAsync(int ddcIndex, DdcConfiguration config, CancellationToken ct)
    {
        // Bereken 32-bit phase word
        // phase = (ulong)(freq × 2^32 / hardware_clock) & 0xFFFFFFFF
        var phaseWord = (uint)(config.FrequencyHz * (1UL << 32) / HardwareClock);

        // Stuur control frame via TCP control channel
        var frame = BuildDdcControlFrame(ddcIndex, phaseWord, config.SampleRateHz, config.DecimationFactor);
        await _tcpControl.SendAsync(frame, ct);
    }

    private void ParseDataFrame(int ddcIndex, ReadOnlySpan<byte> raw)
    {
        // P2 frame header
        var seqNum    = BinaryPrimitives.ReadUInt16BigEndian(raw[0..]);
        var endpoint  = raw[2]; // 0x84 = DDC data
        var ddc       = raw[3];
        var timestampNs = BinaryPrimitives.ReadInt64BigEndian(raw[4..]);
        var bitsPerSample = BinaryPrimitives.ReadInt32BigEndian(raw[8..]);
        var sampleRate = BinaryPrimitives.ReadInt32BigEndian(raw[12..]);
        var numSamples = BinaryPrimitives.ReadInt32BigEndian(raw[16..]);

        // Parse IQ samples (24-bit MSB-first)
        var samples = new float[numSamples * 2];
        for (int i = 0; i < numSamples; i++)
        {
            int offset = 20 + i * 6;
            // 24-bit big-endian signed integer → float
            int iVal = (raw[offset] << 16) | (raw[offset+1] << 8) | raw[offset+2];
            int qVal = (raw[offset+3] << 16) | (raw[offset+4] << 8) | raw[offset+5];
            if ((iVal & 0x800000) != 0) iVal |= unchecked((int)0xFF000000);
            if ((qVal & 0x800000) != 0) qVal |= unchecked((int)0xFF000000);
            samples[i * 2]     = iVal / 8388608f;
            samples[i * 2 + 1] = qVal / 8388608f;
        }

        _rxChannel.Writer.TryWrite(new RxIqFrame(
            DdcIndex: ddc,
            TimestampNs: timestampNs,
            SampleRateHz: sampleRate,
            IqSamples: samples
        ));
    }
    // ... TX, control, etc.
}
```

---

## 7. Backward Compatibility Strategie

### 7.1 Protocol Detectie en Selectie

```
Strategie: prefer P2, fallback to P1

1. Stuur discovery broadcasts parallel (P1 UDP + P2 TCP)
2. Wacht max. 2 seconden op responses
3. Als hardware antwoordt op beide:
   a. Controleer DeviceCapabilities.Protocol2
   b. Selecteer P2 als aanwezig EN geconfigureerd
   c. Fallback naar P1 als P2 mislukt (3 pogingen)
4. Als hardware alleen P1 antwoordt → P1 adapter
5. Sla protocol-keuze op per MAC-adres (LiteDB / settings)
```

### 7.2 Frame Grootte Compatibiliteit

| Protocol | Verwachte frame | Afwijking | Actie |
|---------|----------------|-----------|-------|
| P1 | 512 bytes | deskHPSDR: exact 512 | Valideer exact |
| P1 | 512 bytes | Zeus: 1032 bytes ctrl | Onderscheid ctrl/data frames |
| P2 | 1500 bytes | Zeus: 1444 bytes | Accepteer 1444–1500 range |
| P2 | 1500 bytes | deskHPSDR: 1500 | Standaard |

**Aanbeveling:** NovaSdr accepteert alle frame groottes in bereik [512±16] voor P1 en [1400–1500] voor P2. Gebruik Span<byte>.Length check — geen hardcoded grootte assert.

### 7.3 Sample Rate Backward Compatibility

```
P1 hardware: 48/96/192 kHz (afhankelijk van board firmware)
P2 hardware: 48/96/192/384 kHz

NovaSdr configuratie matrix:
  User selecteert gewenste RX sample rate
  Adapter bepaalt of hardware dit ondersteunt
  Fallback naar lagere rate indien niet ondersteund
  WDSP DSP chain geconfigureerd op actual rate
```

### 7.4 C&C Byte Backward Compatibility (P1)

De C&C byte structuur varieert subtiel per firmware versie en board type. NovaSdr moet:
1. Board-type detecteren uit discovery response
2. De juiste C&C byte layout laden (board-specifieke configuratie)
3. Onbekende boards behandelen als standaard Hermes

---

## 8. Risico's en Onbevestigde Aannames

| # | Risico / Aanname | Bron van onzekerheid | Mitigatie |
|---|-----------------|---------------------|-----------|
| 1 | Zeus 1032-byte "control frame" — dit kan een gecombineerd P1 control+data frame zijn, of een interne buffer structuur | Exacte Zeus source niet beschikbaar | Reverse-engineer via Wireshark trace |
| 2 | Zeus 1444-byte P2 frame — waarom 56 bytes kleiner dan MTU? Kan een specifieke hardware variant zijn | Onbekend | Test met echte hardware; check Zeus changelog |
| 3 | HL2 extension commands in Zeus — niet bevestigd of Zeus HL2 I2C implementeert | Geen feitenbasis | Controleer Zeus.Protocol2 source |
| 4 | Saturn G2 register layout — ongedocumenteerd in beschikbare feiten | Geen detail | Gebruik saturn.c uit deskHPSDR als referentie |
| 5 | P2 MAX_DDC=4 is configuratielimiet, niet hardware-limiet — sommige FPGA boards ondersteunen meer | Spec zegt max 4 in implementaties | Design interface voor tot 8 DDC |
| 6 | Hardware timestamps in P2 — klokdomein en precisie varieert per board | Board firmware-afhankelijk | Behandel timestamp als optioneel (0 = niet beschikbaar) |
| 7 | HL2 ADC clock 76.8 MHz vs standaard 122.88 MHz — phase word berekening verschilt | HL2-specifiek | DetecteerHL2 via board code, pas clock constante aan |
| 8 | Thetis 1032-byte frames — niet bevestigd; mogelijk data uit andere context | Verwarring met Zeus waarden | Thetis NetworkIO.cs analyse vereist |

---

## 9. Capability-based Device Model Ontwerp

### 9.1 Motivatie

In plaats van `if (boardKind == HpsdrBoardKind.HermesLite) { /* HL2 specifieke code */ }` door heel de codebase, gebruikt NovaSdr een capability-based model. Dit is hetzelfde patroon als moderne OS capability checks (POSIX, Android Manifest, etc.).

### 9.2 Capability Detectie

```csharp
public static class HardwareCapabilityDetector
{
    public static DeviceCapabilities Detect(
        HardwareDeviceInfo discoveryInfo,
        HardwareProtocol negotiatedProtocol)
    {
        var caps = DeviceCapabilities.None;

        // Protocol capabilities
        if (negotiatedProtocol == HardwareProtocol.Protocol1)
            caps |= DeviceCapabilities.Protocol1;
        if (negotiatedProtocol == HardwareProtocol.Protocol2)
            caps |= DeviceCapabilities.Protocol2 | DeviceCapabilities.HardwareTimestamp;

        // Board-specific capabilities
        caps |= discoveryInfo.BoardKind switch
        {
            HpsdrBoardKind.HermesLite =>
                DeviceCapabilities.HermesLiteExt | DeviceCapabilities.ExternalI2C,

            HpsdrBoardKind.Saturn =>
                DeviceCapabilities.SaturnExtensions | DeviceCapabilities.MaxDdc4 |
                DeviceCapabilities.SampleRate384k,

            HpsdrBoardKind.Angelia or HpsdrBoardKind.Orion or HpsdrBoardKind.OrionMKII =>
                DeviceCapabilities.DualAdc | DeviceCapabilities.FullDuplex |
                DeviceCapabilities.MaxDdc4,

            HpsdrBoardKind.Hermes or HpsdrBoardKind.HermesII =>
                DeviceCapabilities.FullDuplex,

            _ => DeviceCapabilities.None
        };

        // Feature byte uit discovery response (P2 feature bitmask)
        if (discoveryInfo.Capabilities.HasFlag(DeviceCapabilities.Rx2Stream))
            caps |= DeviceCapabilities.Rx2Stream;

        return caps;
    }
}
```

### 9.3 Capability-based Feature Enabling

```csharp
// In RadioService — geen if(boardKind) checks
public void ConfigureRadio(HardwareDeviceInfo device)
{
    var caps = HardwareCapabilityDetector.Detect(device, _negotiatedProtocol);

    // Tweede ontvanger panel alleen tonen als hardware het ondersteunt
    _uiFeatures.EnableDualReceiver = caps.HasFlag(DeviceCapabilities.DualAdc);

    // 384kHz sample rate optie alleen beschikbaar bij ondersteunde hardware
    _availableSampleRates = caps.HasFlag(DeviceCapabilities.SampleRate384k)
        ? [48000, 96000, 192000, 384000]
        : [48000, 96000, 192000];

    // HL2 ATU control plugin activeren
    if (caps.HasFlag(DeviceCapabilities.InternalAtu))
        _pluginHost.Activate("com.novasdr.hl2-atu");

    // Saturn-specifieke diagnostic plugin
    if (caps.HasFlag(DeviceCapabilities.SaturnExtensions))
        _pluginHost.Activate("com.novasdr.saturn-diagnostics");
}
```

### 9.4 Voordelen van dit Model

| Voordeel | Toelichting |
|----------|-------------|
| Toekomstige hardware | Nieuw board = nieuwe capabilities toevoegen, geen code-wijziging elders |
| Testbaarheid | Mock capabilities in unit tests; geen echte hardware nodig |
| Plugin activatie | Plugins declareren vereiste capabilities; host activeert ze automatisch |
| Graceful degradation | Functie beschikbaar = capability check; geen exceptions op ontbrekende features |
| UI driven by capabilities | Frontend toont/verbergt controls op basis van server-sent capability set |

---

*Einde bestand 03 — Protocol & Hardware Analyse*
