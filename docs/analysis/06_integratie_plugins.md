# 06 — Ham Radio Ecosysteem & Plugin Analyse

> Vastgestelde bronfeiten. Aanbevelingen gebaseerd op bewezen implementaties.
> Doel: plugin architectuur en ecosysteem integratie-strategie voor NovaSdr.

---

## Inhoudsopgave

1. [Feature aanwezigheidsmatrix](#1-feature-aanwezigheidsmatrix)
2. [Plugin architectuur vergelijking](#2-plugin-architectuur-vergelijking)
3. [Zeus plugin systeem — details](#3-zeus-plugin-systeem--details)
4. [Aanbevolen plugin categories voor NovaSdr](#4-aanbevolen-plugin-categories-voor-novasdr)
5. [CAT implementatie aanbeveling](#5-cat-implementatie-aanbeveling)
6. [TCI server aanbeveling](#6-tci-server-aanbeveling)
7. [N1MM integratie als plugin](#7-n1mm-integratie-als-plugin)
8. [DX cluster als plugin](#8-dx-cluster-als-plugin)
9. [WSJT-X / FT8 integratie via VAC/IPC](#9-wsjt-x--ft8-integratie-via-vacipc)
10. [Plugin API uitbreidingen voor multi-device](#10-plugin-api-uitbreidingen-voor-multi-device)
11. [SDRangel channel plugin model als architectuurles](#11-sdrangel-channel-plugin-model-als-architectuurles)

---

## 1. Feature Aanwezigheidsmatrix

| Feature | deskHPSDR (A) | Zeus (B) | Thetis (C) | NovaSdr doel |
|---------|:---:|:---:|:---:|:---:|
| **Protocol & Hardware** | | | | |
| Protocol 1 | Ja | Ja | Ja | Ja |
| Protocol 2 | Ja | Ja | Ja | Ja |
| P1/P2 auto-detect | Nee | Ja | Ja | Ja |
| Hermes-Lite 2 | Beperkt | Onbekend | Ja | Ja |
| Saturn/G2 | Ja (SATURN flag) | Onbekend | Ja | Ja |
| STEMlab/Red Pitaya | Ja (STEMLAB flag) | Onbekend | Ja | Fase 2 |
| **DSP** | | | | |
| WDSP integratie | Ja (statisch) | Ja (dynamisch) | Ja (P/Invoke) | Ja (dynamisch) |
| AGC (multiple modes) | Ja | Ja | Ja | Ja |
| NR / NR2 | Ja | Ja | Ja | Ja |
| NR3/NR4 | Nee | Stubs | Nee | Fase 2 |
| NB / NB2 / SNB | Ja | Ja | Ja | Ja |
| CFC | Ja | Ja | Ja | Ja |
| Graphic EQ (10-band) | Ja | Ja | Ja | Ja |
| Graphic EQ (12-band) | Optioneel (EQ12) | Ja | Ja | Ja |
| **Audio** | | | | |
| PortAudio | Ja | Nee | Ja (fallback) | Nee (miniaudio) |
| PulseAudio | Ja (Linux) | Nee | Nee | Via miniaudio |
| JACK | Nee | Via miniaudio | Nee | Ja (via miniaudio) |
| ASIO | Nee | Nee | Ja (cmASIO) | Fase 2 plugin |
| miniaudio | Nee | Ja | Nee | Ja |
| NAudio | Nee | Nee | Ja | Nee |
| **UI & Display** | | | | |
| Cairo rendering | Ja | Nee | Nee | Nee |
| WebGL rendering | Nee | Ja | Nee | Ja |
| SkiaSharp rendering | Nee | Nee | Ja | Nee |
| Dockable panels | Nee | Ja | Nee | Ja |
| Thema-ondersteuning | Beperkt | Ja | Nee | Ja |
| **Integraties** | | | | |
| TCI server | Ja (tci.c) | Ja (3357L) | Ja (1000L) | Ja |
| CAT / rigctld | Ja (rigctl.c) | Via TCI | Ja (CATCommands.cs) | Ja |
| MIDI control | Optioneel | Gepland | Via Midi2Cat | Ja |
| N1MM UDP spectrum | Nee | Onbekend | Ja (N1MM.cs) | Ja (plugin) |
| DX cluster TCP | Ja (dxcluster.c) | Via plugin | Ja | Ja (plugin) |
| CW keyer (iambic) | Ja (iambic.c) | Onbekend | Ja | Ja |
| Solar / propagatie | Ja (libsolar) | Onbekend | Onbekend | Ja (plugin) |
| Discord bot | Nee | Nee | Ja (clsDiscord.cs) | Optioneel plugin |
| ADIF log export | Onbekend | Onbekend | Ja | Ja (plugin) |
| WSJT-X audio routing | Nee | Onbekend | Via VAC | Ja (plugin) |
| **Plugin systeem** | | | | |
| Formeel plugin API | Nee | Ja | Nee | Ja |
| Plugin manifest | Nee | Ja (plugin.json) | Nee | Ja |
| VST3 bridge | Nee | Ja (bridge) | Nee | Fase 3 |
| Plugin manager UI | Nee | Onbekend | Nee | Ja |
| **Overig** | | | | |
| IQ recording | Onbekend | Onbekend | Onbekend | Fase 2 |
| Spectrum recording | Onbekend | Onbekend | Ja | Fase 2 |
| Remote operation | Beperkt | Ja (WebSocket) | Nee | Ja |
| Multi-hardware | Nee | Gepland | Beperkt | Fase 3 |

---

## 2. Plugin Architectuur Vergelijking

### 2.1 deskHPSDR — Geen Formele Plugin API

```
deskHPSDR gebruikt compile-time feature flags als "plugin-mechanisme":
  -DMIDI      → MIDI ondersteuning ingebakken
  -DSATURN    → Saturn hardware code pad
  -DSTEMLAB   → Red Pitaya ondersteuning
  -DAUTOGAIN  → Auto gain feature
  -DEQ12      → 12-band EQ in plaats van 10-band

Gevolgen:
  - Geen runtime-laden van features
  - Geen community plugins mogelijk
  - Feature matrix = product van bool-combinaties (2^n varianten)
  - Testing vereist alle 2^n builds

Ecosysteem-integraties (ingebakken modules, geen plugins):
  tci.c:         TCI server (libwebsockets)
  rigctl.c:      CAT rigctld protocol
  dxcluster.c:   DX cluster TCP client
  iambic.c:      CW iambic keyer
  libsolar/:     Solar propagation data
```

### 2.2 Zeus — Formeel Plugin Systeem

```
Zeus heeft een volledig gedocumenteerd plugin systeem:

Componenten:
  IZeusPlugin interface   — contractdefinitie
  plugin.json manifest    — metadata en capabilities
  ABI versioning          — backwards compatibility guarantee
  VST3 bridge             — speciale plugin type voor audio effects
  Plugin host             — laad/unlaad lifecycle management

Plugin types (uit bronfeiten):
  "vst3"   → VST3 audio processing plugin
  "ui"     → React panel component
  "dsp"    → Server-side DSP insert
  "io"     → Hardware I/O uitbreiding
  (overige types geschat op basis van architectuur)

Deployment:
  Plugin directory scanning op startup
  Hot-reload mogelijk (afhankelijk van type)
  .NET 10 AssemblyLoadContext voor isolatie
```

### 2.3 Thetis — Geen Formele Plugin API

```
Thetis heeft extern een aantal "module"-like bestanden:
  CATCommands.cs:    7000+ regels, Kenwood-compat CAT
  N1MM.cs:           1500 regels, UDP spectrum streaming
  TCIServer.cs:      1000 regels, TCI server
  clsDiscord.cs:     Discord.Net bot
  Midi2Cat project:  apart Solution project voor MIDI

Maar: geen formele plugin interface, geen manifest, geen lifecycle API.
Alles is ofwel ingebakken in console.cs, of een vaste dependency.

Midi2Cat is het dichtst bij een "plugin":
  Separaat project in de Solution
  Communiceert via Windows IPC / UDP met Thetis
  Dit is het "poor-man's plugin" patroon
```

---

## 3. Zeus Plugin Systeem — Details

### 3.1 IZeusPlugin Interface

Op basis van de architectuur van Zeus v0.1 (april 2026) kan de plugin interface als volgt worden gereconstrueerd:

```csharp
// Zeus.Plugins.Abstractions

/// <summary>
/// Basis-interface voor alle Zeus plugins.
/// </summary>
public interface IZeusPlugin : IAsyncDisposable
{
    /// <summary>Unieke plugin identifier (reverse-DNS stijl).</summary>
    string PluginId { get; }

    /// <summary>Plugin versie (SemVer).</summary>
    Version Version { get; }

    /// <summary>Capabilities die de plugin vereist van de host.</summary>
    IReadOnlyList<string> RequiredCapabilities { get; }

    /// <summary>Capabilities die de plugin biedt aan andere plugins.</summary>
    IReadOnlyList<string> ExposedCapabilities { get; }

    /// <summary>Initialize plugin met host services.</summary>
    Task InitializeAsync(IZeusPluginHost host, CancellationToken ct);

    /// <summary>Start plugin (na initialisatie).</summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>Stop plugin (voor dispose).</summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>Plugin health status.</summary>
    PluginStatus Status { get; }
}

public enum PluginStatus { Loading, Running, Degraded, Stopped, Faulted }
```

### 3.2 Plugin Manifest (plugin.json)

```json
{
  "pluginId": "com.zeus.dxcluster",
  "name": "DX Cluster Client",
  "version": "1.2.0",
  "author": "Zeus Project",
  "description": "DX cluster TCP client met bandmap integratie",
  "type": "service",
  "requiredCapabilities": [
    "novasdr.network",
    "novasdr.spectrum.markers"
  ],
  "exposedCapabilities": [
    "novasdr.dxspots"
  ],
  "abiVersion": "1",
  "entryAssembly": "Zeus.Plugin.DxCluster.dll",
  "entryType": "Zeus.Plugin.DxCluster.DxClusterPlugin",
  "settings": {
    "host": { "type": "string", "default": "dxc.db0sue.de" },
    "port": { "type": "integer", "default": 7300 },
    "callsign": { "type": "string", "required": true }
  },
  "uiComponents": [
    {
      "componentId": "cluster-panel",
      "label": "DX Cluster",
      "defaultSize": { "w": 4, "h": 6 },
      "module": "./dist/ClusterPanel.js"
    }
  ]
}
```

### 3.3 IZeusPluginHost (Host API)

```csharp
/// <summary>
/// Services die de plugin host aanbiedt aan plugins.
/// </summary>
public interface IZeusPluginHost
{
    // Radio state
    IObservable<RadioState> RadioStateChanged { get; }
    Task SetFrequencyAsync(int channel, double frequencyHz);
    Task SetModeAsync(int channel, RadioMode mode);

    // Spectrum markers (voor DX spots, beacons, etc.)
    void AddSpectrumMarker(SpectrumMarker marker);
    void RemoveSpectrumMarker(string markerId);

    // Audio routing (voor VAC-achtige functionaliteit)
    IAudioStream GetRxAudioStream(int channel);
    IAudioStream GetTxAudioStream(int channel);

    // Settings persistence
    T GetSetting<T>(string key, T defaultValue);
    void SetSetting<T>(string key, T value);

    // Event broadcasting (naar alle WebSocket clients)
    Task BroadcastEventAsync(string eventType, object payload);

    // Plugin-to-plugin communicatie
    bool TryGetPluginService<T>(string pluginId, out T service);

    // Logging
    ILogger GetLogger(string pluginId);

    // Hardware capabilities
    DeviceCapabilities HardwareCapabilities { get; }
}
```

### 3.4 ABI Versioning Strategie

```
Zeus gebruikt ABI versioning voor backward compatibility:
  plugin.json: "abiVersion": "1"

Versioning semantiek:
  abiVersion 1: initiële Zeus plugin API
  abiVersion 2: breaking changes (nieuwe vereiste methoden)
  
NovaSdr strategie:
  Behoud abiVersion 1 plugins werkend via adapter shim
  Deprecation cycle: 2 major versies (12 maanden)
  
Richtlijn:
  IZeusPlugin interface mag methods NIET verwijderen
  Nieuwe methods: default implementatie (interface default methods)
  Breaking change = nieuwe abiVersion + migration guide
```

---

## 4. Aanbevolen Plugin Categories voor NovaSdr

### 4.1 Core Plugins (meegeleverd met NovaSdr)

Deze plugins zijn onderdeel van de NovaSdr distributie en niet optioneel te verwijderen zonder functionaliteitsverlies.

| Plugin ID | Naam | Beschrijving | Basis: |
|-----------|------|-------------|--------|
| `com.novasdr.tci` | TCI Server | TCI protocol server (3357L Zeus als basis) | Zeus Tci/ |
| `com.novasdr.cat` | CAT Server | Kenwood K3S-compat CAT via TCP/serial | Thetis CATCommands.cs |
| `com.novasdr.cw` | CW Keyer | Iambic keyer, side-tone oscillator | deskHPSDR iambic.c |
| `com.novasdr.audio-routing` | Audio Routing | RX/TX audio device configuratie | miniaudio |
| `com.novasdr.hardware` | Hardware Manager | P1/P2 discovery, adapter lifecycle | Zeus Protocol1/2Client |
| `com.novasdr.spectrum` | Spectrum Engine | WDSP analyzer, WebSocket broadcast | Zeus + WDSP |

### 4.2 Ecosystem Plugins (optioneel, meegeleverd)

Officieel ondersteund door het NovaSdr project, optioneel te activeren.

| Plugin ID | Naam | Beschrijving | Basis: |
|-----------|------|-------------|--------|
| `com.novasdr.dxcluster` | DX Cluster | TCP DX cluster client, bandmap | deskHPSDR dxcluster.c |
| `com.novasdr.n1mm` | N1MM Logger | UDP spectrum streaming naar N1MM | Thetis N1MM.cs |
| `com.novasdr.midi` | MIDI Control | VFO/function MIDI mapping | deskHPSDR midi.c |
| `com.novasdr.propagation` | Propagation | SFI/K/A indices, band conditions | deskHPSDR libsolar/ |
| `com.novasdr.adif-log` | ADIF Logger | QSO logging met ADIF export | nieuw |
| `com.novasdr.wsjtx-bridge` | WSJT-X Bridge | Audio loopback voor FT8/FT4/JT65 | nieuw |
| `com.novasdr.hl2-atu` | HL2 ATU | Hermes-Lite 2 ATU control | HL2 spec |
| `com.novasdr.saturn-diag` | Saturn Diag | Saturn/G2 hardware diagnostics | deskHPSDR saturn.c |

### 4.3 Community Plugins (third-party)

Niet meegeleverd; gedownload via plugin repository.

```
Verwachte community plugins:
  - Skimmer integratie (CW Skimmer, RTTY Skimmer)
  - RBN (Reverse Beacon Network) client
  - PSK Reporter integratie
  - APRS.fi live overlay
  - HamAlert push notificaties
  - CloudLog / Ham Radio Deluxe log synchronisatie
  - WSJT-X auto-configurator (geavanceerder dan basis bridge)
  - VARA modem bridge (HF packet)
  - Fldigi bridge (diverse digitale modes)
  - Discord bot (gebaseerd op Thetis clsDiscord.cs)
  - Telegram notificaties
  - Voice keyer (audio macro player)
  - Band plan overlay editor
  - Antenna switching controller (Arduino/Raspberry Pi)
```

**Plugin repository:**
```
Gehoste repository (NovaSdr Hub):
  REST API: GET /plugins?category=&platform=&verified=true
  Manifest: naam, versie, author, vereisten, screenshots
  Code signing: verplicht voor "verified" badge
  Community ratings + reviews
```

---

## 5. CAT Implementatie Aanbeveling

### 5.1 Thetis CATCommands.cs als Referentie

```
CATCommands.cs bevat 7000+ regels Kenwood-compatibele CAT implementatie.
Dit is de meest complete ham-radio CAT referentie beschikbaar in open-source.

Kenwood TS-2000 / K3S protocol subset voor NovaSdr:
  Prioriteit 1 (MVP):
    IF;        — Informatie (frequentie, mode, etc.)
    FA;        — VFO A frequentie instellen/lezen
    FB;        — VFO B frequentie instellen/lezen
    MD;        — Mode instellen/lezen
    PS;        — Power on/off
    TX;        — Transmit aan/uit
    RX;        — Receive
    AG;        — AF gain
    RF;        — RF gain
    SQ;        — Squelch niveau
    SM;        — S-meter lezen
    AC;        — Antenna tuner
    
  Prioriteit 2 (Fase 2):
    AN;        — Antenna selectie
    FT;        — VFO selectie voor TX (split)
    GT;        — AGC tijdconstante
    IS;        — IF shift
    NB;        — Noise blanker
    NR;        — Noise reduction
    PA;        — Preamp aan/uit
    RA;        — Attenuator
    RG;        — RF gain (alternatief)
    RL;        — NR level
    VS;        — VFO swap
    
  Uitbreidingen (NovaSdr-specific):
    XSPECT;    — Spectrum data request (custom)
    XPROP;     — Propagation data (custom)
    XSPOT;     — DX spot injectie (custom)
```

### 5.2 CAT Transport Layer

```
NovaSdr CAT plugin ondersteunt meerdere transports:

1. TCP socket (Kenwood TS-2000 network mode):
   Poort: 4532 (rigctld standaard)
   Protocol: Hamlib rigctld compatible
   Meerdere gelijktijdige verbindingen: ja
   
2. Hamlib rigctld compatibiliteit:
   NovaSdr als rig model in Hamlib via rigctld
   Stelt alle Hamlib-ondersteunde software in staat te verbinden
   (WSJT-X, fldigi, N1MM, etc.)
   
3. Serial COM port (legacy CAT):
   Via USB-serial adapter
   9600/19200/38400/57600 baud
   Voor hardware controllers en legacy loggers

4. TCI (zie TCI sectie):
   TCI is de modernere CAT vervanging
   Overlap in features maar TCI is bi-directioneel event-driven
```

### 5.3 CAT Plugin Interface

```csharp
// NovaSdr CAT plugin interface
// Intern interface voor CATCommands.cs implementatie

public interface ICatCommandProcessor
{
    /// <summary>
    /// Verwerk inkomend CAT commando, retourneer response.
    /// </summary>
    string? ProcessCommand(string command);

    /// <summary>
    /// Registreer custom commando handler (voor plugin uitbreidingen).
    /// </summary>
    void RegisterCustomCommand(string prefix, Func<string, string?> handler);

    /// <summary>
    /// Event: CAT commando ontvangen (voor logging/debugging).
    /// </summary>
    IObservable<CatCommandEvent> Commands { get; }
}

public record CatCommandEvent(
    string Raw,
    string Command,
    string? Argument,
    string? Response,
    DateTime Timestamp
);
```

### 5.4 NovaSdr CAT Aanbeveling Samenvatting

```
1. Implementeer Kenwood K3S subset als de minimale CAT baseline
   (maximale compatibiliteit met populaire software)
   
2. Gebruik Thetis CATCommands.cs als lexicaal referentiedocument
   (niet overnemen — verwijzingsbron voor commando-syntax)
   
3. Maak rigctld-compatibel op poort 4532
   (automatische interoperabiliteit met WSJT-X, fldigi, N1MM, etc.)
   
4. CAT als plugin met uitbreidbare command registratie
   (plugins kunnen eigen CAT commando's toevoegen)
   
5. Volledige command logging naar debug output
   (essentieel voor support en plugin ontwikkeling)
```

---

## 6. TCI Server Aanbeveling

### 6.1 TCI Protocol Overzicht

**TCI (Transceiver Control Interface)** is een modern, open protocol ontwikkeld door Expert Electronics. Het is een WebSocket-gebaseerd JSON+binary protocol voor radio control en spectrum data.

TCI kenmerken:
- WebSocket transport (JSON control messages + binary audio/spectrum)
- Bi-directioneel event-driven (geen polling)
- Spectrum data streaming ingebakken
- Audio streaming ingebakken
- Meer expresief dan Kenwood CAT

### 6.2 TCI Server per Project

```
deskHPSDR tci.c:
  libwebsockets gebaseerd
  C implementatie
  Geen exacte regelcount beschikbaar, maar aanwezig

Zeus Zeus.Server.Hosting/Tci/:
  3357 regels — de meest complete TCI implementatie
  ASP.NET Core WebSocket server
  Moderne async/await .NET implementatie
  
Thetis TCIServer.cs:
  1000 regels — basis TCI implementatie
  WinForms synchrone aanpak
```

### 6.3 Zeus Tci/ als Basis

**Zeus TCI implementatie (3357 regels) is de aanbevolen basis voor NovaSdr:**

```
Motivatie:
  1. Meest complete implementatie van de drie
  2. .NET 10 async/await — compatibel met NovaSdr server
  3. ASP.NET Core WebSocket — zelfde middleware als rest van NovaSdr
  4. Actief onderhouden (v0.1, april 2026)
  
Aanbevolen aanpak:
  Port Zeus.Server.Hosting/Tci/ naar NovaSdr.Server.Tci
  Ontkoppel van Zeus-specifieke types naar NovaSdr interfaces
  Voeg plugin hook toe zodat plugins TCI commando's kunnen registreren
```

### 6.4 TCI Plugin Interface voor NovaSdr

```csharp
// NovaSdr TCI uitbreidingspunten

public interface ITciCommandExtension
{
    /// <summary>
    /// TCI commando namen die deze extensie afhandelt.
    /// </summary>
    IReadOnlyList<string> HandledCommands { get; }

    /// <summary>
    /// Verwerk inkomend TCI commando.
    /// </summary>
    Task<TciResponse?> HandleAsync(string command, JsonElement? arguments, CancellationToken ct);
}

public interface ITciEventEmitter
{
    /// <summary>
    /// Stuur TCI event naar alle verbonden clients.
    /// </summary>
    Task EmitAsync(string eventName, object? payload = null, CancellationToken ct = default);
}

// Plugin registreert TCI extensie via host:
// host.RegisterTciExtension(new MyPluginTciExtension());
```

### 6.5 TCI Spectrum Streaming

```
NovaSdr TCI spectrum data flow:

Server:
  WDSP GetPixels() @ 30Hz
  → float[] dB pixels (analyzerFftSize punten)
  → gzip compress
  → WebSocket binary frame (TCI spectrum frame type)
  → broadcast naar alle TCI clients

Client (bijv. DX Atlas, Logger32):
  WebSocket onbinary
  → decompress
  → spectrum weergave of spot detectie

TCI spectrum frame format:
  Byte 0:     frame type (TCI spec)
  Byte 1-4:   DDC index + sample rate
  Byte 5-8:   center frequentie (Hz, int32)
  Byte 9-N:   float32 array (dB, N = fftSize punten)
```

---

## 7. N1MM Integratie als Plugin

### 7.1 N1MM UDP Spectrum Protocol

```
N1MM Logger+ ontvangt spectrum data via UDP:
  Protocol: UDP unicast of multicast
  Poort: 12060 (N1MM standaard)
  Frame: XML payload met spectrum data
  
XML frame formaat (vereenvoudigd):
  <SpectrumData>
    <timestamp>1234567890</timestamp>
    <centerFrequency>14225000</centerFrequency>
    <sampleRate>192000</sampleRate>
    <data>base64_encoded_spectrum_bytes</data>
    <dataType>FFT</dataType>
  </SpectrumData>

Thetis N1MM.cs (1500 regels) implementeert dit volledig.
```

### 7.2 N1MM Plugin voor NovaSdr

```csharp
// com.novasdr.n1mm plugin

[NovaSdrPlugin(Id = "com.novasdr.n1mm", Version = "1.0.0")]
public class N1mmPlugin : IZeusPlugin
{
    private readonly IZeusPluginHost _host;
    private UdpClient? _udpClient;
    private PeriodicTimer? _broadcastTimer;

    public async Task StartAsync(CancellationToken ct)
    {
        var targetHost = _host.GetSetting("n1mm.host", "127.0.0.1");
        var targetPort = _host.GetSetting("n1mm.port", 12060);
        var fftSize    = _host.GetSetting("n1mm.fftSize", 4096);

        _udpClient = new UdpClient();
        _udpClient.Connect(targetHost, targetPort);

        _broadcastTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(100)); // 10Hz
        _ = BroadcastLoopAsync(fftSize, ct);
    }

    private async Task BroadcastLoopAsync(int fftSize, CancellationToken ct)
    {
        while (await _broadcastTimer!.WaitForNextTickAsync(ct))
        {
            // Haal spectrum pixels op van host
            if (!_host.TryGetSpectrumPixels(channel: 0, out var pixels))
                continue;

            var xml = BuildN1mmXmlFrame(pixels, _currentFrequency);
            var data = Encoding.UTF8.GetBytes(xml);
            await _udpClient.SendAsync(data, ct);
        }
    }

    private static string BuildN1mmXmlFrame(ReadOnlySpan<float> pixels, double centerFreq)
    {
        // Converteer float[] naar base64 encoded bytes
        var bytes = MemoryMarshal.AsBytes(pixels).ToArray();
        return $"""
            <SpectrumData>
              <timestamp>{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}</timestamp>
              <centerFrequency>{(long)centerFreq}</centerFrequency>
              <sampleRate>192000</sampleRate>
              <dataType>FFT</dataType>
              <data>{Convert.ToBase64String(bytes)}</data>
            </SpectrumData>
            """;
    }
}
```

### 7.3 N1MM Plugin Settings (plugin.json fragment)

```json
{
  "settings": {
    "host": {
      "type": "string",
      "default": "127.0.0.1",
      "label": "N1MM Host IP",
      "description": "IP-adres van N1MM Logger+ instantie"
    },
    "port": {
      "type": "integer",
      "default": 12060,
      "label": "UDP Poort"
    },
    "fftSize": {
      "type": "integer",
      "default": 4096,
      "enum": [1024, 2048, 4096, 8192],
      "label": "FFT grootte voor N1MM"
    },
    "updateRate": {
      "type": "integer",
      "default": 10,
      "min": 1,
      "max": 30,
      "label": "Updates per seconde"
    }
  }
}
```

---

## 8. DX Cluster als Plugin

### 8.1 DX Cluster Protocol

```
DX cluster communicatie via DX Spider / AR-Cluster:
  Transport: TCP socket
  Protocol: telnet-like text protocol
  Standaard poort: 7300
  
Verbindingsflow:
  1. TCP connect naar cluster (bijv. dxc.db0sue.de:7300)
  2. Login: stuur callsign + Enter
  3. Ontvang welkomstbericht
  4. Lees binnenkomende DX spots (continua stream)
  5. Optioneel: stuur commando's (SET/DX, SHOW/DX, etc.)

DX spot format (AR-Cluster):
  "DX de PA0ABC:  14.225.50  DL1ABC         15:32 28-May-26 EU <EU>"
  
  Parse: callsign van spotter, frequentie, DX station, tijd, commentaar
```

### 8.2 DX Cluster Plugin Architectuur

```csharp
[NovaSdrPlugin(Id = "com.novasdr.dxcluster", Version = "1.0.0")]
public class DxClusterPlugin : IZeusPlugin
{
    private TcpClient? _tcpClient;
    private readonly List<DxSpot> _spotCache = new();

    public async Task StartAsync(CancellationToken ct)
    {
        var host = _host.GetSetting("cluster.host", "dxc.db0sue.de");
        var port = _host.GetSetting("cluster.port", 7300);
        var call = _host.GetSetting("cluster.callsign", "");

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(host, port, ct);

        _ = ReadLoopAsync(_tcpClient.GetStream(), call, ct);
    }

    private async Task ReadLoopAsync(NetworkStream stream, string callsign, CancellationToken ct)
    {
        var reader = new StreamReader(stream);
        bool loggedIn = false;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;

            if (!loggedIn && line.Contains("login:"))
            {
                await new StreamWriter(stream).WriteLineAsync(callsign);
                loggedIn = true;
                continue;
            }

            if (TryParseDxSpot(line, out var spot))
            {
                _spotCache.Add(spot);
                PruneOldSpots();

                // Voeg marker toe aan spectrum
                _host.AddSpectrumMarker(new SpectrumMarker(
                    Id:        $"spot-{spot.DxCall}-{spot.FrequencyHz}",
                    FreqHz:    spot.FrequencyHz,
                    Label:     spot.DxCall,
                    Color:     ClassifySpotColor(spot),
                    ExpiresAt: DateTime.UtcNow.AddMinutes(15)
                ));

                // Broadcast event naar frontend
                await _host.BroadcastEventAsync("dx-spot", spot);
            }
        }
    }

    private static bool TryParseDxSpot(string line, out DxSpot spot)
    {
        // Parse "DX de CALLER:  FREQ  DXCALL  TIME  DATE  COMMENT"
        spot = default;
        if (!line.StartsWith("DX de ")) return false;

        // ... parsing logica (gebaseerd op deskHPSDR dxcluster.c referentie)
        return true;
    }
}

public record DxSpot(
    string SpotterCall,
    string DxCall,
    double FrequencyHz,
    string Comment,
    DateTime SpottedAt,
    DxccEntity? Entity
);
```

### 8.3 DX Cluster Filtering (Plugin Settings)

```json
{
  "settings": {
    "host":      { "type": "string",  "default": "dxc.db0sue.de" },
    "port":      { "type": "integer", "default": 7300 },
    "callsign":  { "type": "string",  "required": true },
    "bands":     {
      "type": "array",
      "items": { "type": "string", "enum": ["160m","80m","40m","20m","17m","15m","12m","10m","6m","2m"] },
      "default": ["20m","15m","10m"]
    },
    "modes":     {
      "type": "array",
      "items": { "type": "string" },
      "default": ["SSB","CW","FT8"]
    },
    "showOwnContinent": { "type": "boolean", "default": true },
    "highlightDxcc":    { "type": "boolean", "default": true },
    "maxSpotAge":       { "type": "integer", "default": 30, "unit": "minutes" }
  }
}
```

---

## 9. WSJT-X / FT8 Integratie via VAC/IPC

### 9.1 Het Probleem

```
WSJT-X (Joe Taylor K1JT) verwacht:
  1. Audio input: RX IQ → gedemoduleerde audio via virtuele audio kabel
  2. Audio output: TX audio → microfoon input van radio (via VAC)
  3. CAT control: WSJT-X verstuurt frequentie via Kenwood CAT
  4. PTT: rigctld of RTS/DTR via serieel
  
Traditionele aanpak (Thetis):
  Virtual Audio Cable (VAC) Windows-driver
  Loopback audio device
  Windows-only, vereist $40 driver
```

### 9.2 WSJT-X Bridge Plugin

```
NovaSdr WSJT-X bridge aanpak (cross-platform):

Optie A: loopback audio device (platform-specific)
  Windows: VB-Audio Virtual Cable (gratis) of Windows native loopback
  macOS:   BlackHole (gratis, Existential Audio)
  Linux:   JACK loopback (standaard beschikbaar)
  
  Plugin configureert NovaSdr audio output → virtual device
  WSJT-X leest van zelfde virtual device als audio input
  
Optie B: IPC audio pipe (platform-agnostisch)
  Plugin exposet named pipe / UNIX socket als audio device
  WSJT-X partner-tool leest audio van pipe en voert in via virtual device
  
Optie C: WSJT-X protocol integratie
  WSJT-X UDP protocol (localhost:2237) voor spot reporting
  NovaSdr leest WSJT-X decoded spots via UDP broadcast
  Geen audio routing nodig voor spot-only integratie
  
Aanbeveling: Optie C (spot-only) voor MVP, Optie A met platform-gidsen voor fase 2
```

### 9.3 WSJT-X UDP Spot Protocol (Optie C)

```csharp
// WSJT-X gebruikt UDP broadcast op poort 2237 voor decoded spots
// Format: WSJT-X Multicast protocol (Google Protocol Buffers variant)

[NovaSdrPlugin(Id = "com.novasdr.wsjtx-spots")]
public class WsjtxSpotsPlugin : IZeusPlugin
{
    private const int WsjtxUdpPort = 2237;
    private UdpClient? _listener;

    public async Task StartAsync(CancellationToken ct)
    {
        _listener = new UdpClient(WsjtxUdpPort);
        _ = ListenLoopAsync(ct);
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await _listener!.ReceiveAsync(ct);
            if (TryParseWsjtxMessage(result.Buffer, out var spots))
            {
                foreach (var spot in spots)
                {
                    // Voeg FT8 contact toe als spectrum marker
                    _host.AddSpectrumMarker(new SpectrumMarker(
                        Id:    $"ft8-{spot.Call}",
                        FreqHz: spot.FrequencyHz,
                        Label: $"{spot.Call} {spot.DbSnr:+#;-#}dB",
                        Color: SpotColorForSnr(spot.DbSnr)
                    ));

                    await _host.BroadcastEventAsync("ft8-decode", spot);
                }
            }
        }
    }
}
```

---

## 10. Plugin API Uitbreidingen voor Multi-device

### 10.1 Multi-device Use Cases

```
Use case 1: Twee HPSDR radios gelijktijdig
  Radio A: 20m SSB QSO
  Radio B: 40m monitoring
  Beide VFO's zichtbaar, onafhankelijk DSP per radio

Use case 2: Diversity receive
  Twee antennes → twee ADC's op hetzelfde board (Angelia/Orion)
  Gecombineerde IQ streams voor MRC diversity
  Vereist: gesynchroniseerde ADC clocks (P2 hardware timestamp)

Use case 3: SO2R (Single Operator 2 Radio)
  Contest operating met twee radios
  Automatisch audio switching (R1 TX → R2 mute, vice versa)
  N1MM SO2R plugin integratie

Use case 4: Remote + local
  Server op locatie, meerdere clients thuis
  Authenticatie + per-client resource allocatie
```

### 10.2 Multi-device Plugin API Extensions

```csharp
// Uitbreidingen op IZeusPluginHost voor multi-device

public interface IZeusPluginHost // Uitgebreid
{
    // Bestaande: één radio (channel 0)

    // Nieuw voor multi-device:

    /// <summary>Alle verbonden hardware adapters.</summary>
    IReadOnlyList<IHardwareAdapter> ConnectedDevices { get; }

    /// <summary>Specifieke RX stream van hardware adapter + DDC index.</summary>
    IAsyncEnumerable<RxIqFrame> ReceiveRx2Stream(
        string deviceMacAddress,
        int ddcIndex,
        CancellationToken ct = default);

    /// <summary>Stuur control naar specifieke hardware adapter.</summary>
    Task ControlRx2Async(
        string deviceMacAddress,
        int ddcIndex,
        DdcConfiguration config,
        CancellationToken ct = default);

    /// <summary>Audio routing: koppel DSP output aan specifiek audio device.</summary>
    Task RouteAudioAsync(
        int dspChannel,
        string audioDeviceId,
        AudioRouteOptions options,
        CancellationToken ct = default);

    /// <summary>Evento: hardware device verbonden/verbroken.</summary>
    IObservable<DeviceConnectionEvent> DeviceConnections { get; }
}

public record AudioRouteOptions(
    float Gain = 1.0f,
    bool Mute = false,
    PanPosition Pan = PanPosition.Center
);

public enum PanPosition { Left, Center, Right }

public record DeviceConnectionEvent(
    HardwareDeviceInfo Device,
    DeviceConnectionState State,
    DateTime Timestamp
);
```

### 10.3 SO2R Plugin (schets)

```csharp
[NovaSdrPlugin(
    Id = "com.novasdr.so2r",
    RequiredCapabilities = ["novasdr.multi-device", "novasdr.audio-routing"]
)]
public class So2rPlugin : IZeusPlugin
{
    public async Task StartAsync(CancellationToken ct)
    {
        var devices = _host.ConnectedDevices;
        if (devices.Count < 2)
        {
            _host.GetLogger(PluginId).LogWarning("SO2R vereist twee verbonden radios");
            return;
        }

        // Abonneer op PTT state changes
        _host.RadioStateChanged
            .Select(s => s.Ptt)
            .DistinctUntilChanged()
            .Subscribe(OnPttChanged);

        // Registreer SO2R panel in frontend
        await _host.BroadcastEventAsync("plugin-panel-register", new
        {
            id = "so2r-panel",
            title = "SO2R Control",
            componentId = "so2r-panel"
        });
    }

    private void OnPttChanged(bool ptt)
    {
        if (ptt)
        {
            // Radio A transmit → mute Radio B audio
            _ = _host.RouteAudioAsync(channel: 1, audioDeviceId: "default",
                new AudioRouteOptions(Mute: true));
        }
        else
        {
            // Radio A receive → unmute Radio B
            _ = _host.RouteAudioAsync(channel: 1, audioDeviceId: "default",
                new AudioRouteOptions(Mute: false));
        }
    }
}
```

---

## 11. SDRangel Channel Plugin Model als Architectuurles

### 11.1 SDRangel Plugin Architectuur

**SDRangel** (Edouard Griffiths F4EXB) is een geavanceerde open-source SDR applicatie met een volwassen channel plugin architectuur.

```
SDRangel channel plugin model:

  DeviceSet (hardware radio)
    ├── DeviceAPI (hardware abstraction)
    ├── BasebandSampleSink (hoofdontvanger stream)
    └── ChannelAPI[] (plugins):
          ├── SSBDemod       → SSB demodulator channel
          ├── NFMDemod       → Narrowband FM channel
          ├── WFMDemod       → Wideband FM channel
          ├── FT8Demod       → FT8 decoder channel
          ├── AISDemod       → AIS (scheepvaart) decoder channel
          ├── DSD            → Digital voice decoder
          ├── LoRaDemod      → LoRa protocol decoder
          └── [user plugins] → Community channels

Elk channel plugin:
  - Ontvangt IQ sample stream van basebanddemodulator
  - Heeft eigen frequentie-offset (NCO in software)
  - Heeft eigen DSP chain (demodulator, decoder)
  - Heeft eigen UI widget (Qt)
  - Kan audio outputten, data decoden, of spectrum genereren
```

### 11.2 Lessen voor NovaSdr

```
Les 1: Channel plugins zijn orthogonale DSP chains
  In SDRangel kan één hardware radio meerdere gelijktijdige channels hebben.
  Bijv.: 20m LSB QSO + naburig CW skimmer + FT8 decoder op dezelfde band.
  
  NovaSdr implicatie:
    Elk DDC channel (0-3) kan meerdere software channel plugins hebben.
    Channel plugin ontvangt IQ stream direct van IDspEngine.
    
  Architectuur:
    IHardwareAdapter.ReceiveAsync()
      → ChannelRouter (multiplexed per DDC)
        → [DdcChannel 0] → IChannelPlugin[]
            ├── SsbDemodPlugin
            ├── CwSkimmerPlugin (extra DSP)
            └── Ft8DecoderPlugin
        → [DdcChannel 1] → IChannelPlugin[]
            └── FmDemodPlugin

Les 2: Frequency offset per channel (geen extra NCO hardware nodig)
  Elk software channel kan een frequentie-offset hebben t.o.v. het DDC center.
  Dit stelt meerdere decoders in staat op verschillende frequenties te werken
  zonder extra hardware DDC's.
  
  NovaSdr uitbreiding op IZeusPlugin:
    IChannelPlugin : IZeusPlugin
    {
        double FrequencyOffsetHz { get; set; }  // Relatief aan DDC center
        void Process(ReadOnlySpan<float> iq);   // Eigen DSP
    }

Les 3: Plugin GUI is geïsoleerd van plugin DSP
  SDRangel Qt GUI widget is een aparte klasse van de DSP ChannelAPI.
  Dit stelt headless deployment toe (server zonder UI).
  
  NovaSdr implicatie:
    Server-side plugin (DSP) heeft geen React code.
    React UI componenten worden apart geleverd als .js bundles in plugin.json.
    Server-side plugin = .NET DLL.
    Frontend plugin = React component bundle.
    Deze zijn losjes gekoppeld via WebSocket events.

Les 4: Sample rate conversie per channel
  SDRangel heeft per-channel resamplers.
  Een 2 MHz basisbandstream kan worden gereduceerd tot 48kHz per demodulator channel.
  
  NovaSdr implicatie:
    IDspEngine.SetDecimation(channel, factor) voor efficiënte multi-channel DSP.
    Channels met lage bandbreedte (CW: 500Hz) vereisen hoge decimation.
    Channels met hoge bandbreedte (FM: 200kHz) vereisen lage decimation.
```

### 11.3 SDRangel-geïnspireerde NovaSdr Channel Plugin Interface

```csharp
// Uitbreiding van plugin model met channel plugin concept

/// <summary>
/// Channel plugin: ontvangt IQ samples op een specifieke frequentie
/// en verwerkt ze onafhankelijk van de hoofd-DSP chain.
/// </summary>
public interface IChannelPlugin : IZeusPlugin
{
    /// <summary>Frequentie-offset t.o.v. DDC center frequentie.</summary>
    double FrequencyOffsetHz { get; set; }

    /// <summary>Vereiste bandbreedte (bepaalt decimation factor).</summary>
    double RequiredBandwidthHz { get; }

    /// <summary>Process IQ samples (na decimation naar RequiredBandwidthHz).</summary>
    void ProcessSamples(int ddcIndex, ReadOnlySpan<float> iqSamples, int sampleRateHz);

    /// <summary>Geef geproduceerde audio output (null als channel geen audio heeft).</summary>
    bool TryGetAudioOutput(Span<float> audioBuffer, out int samplesWritten);
}

// Voorbeeld: CW Skimmer channel plugin
[NovaSdrPlugin(Id = "com.novasdr.cw-skimmer")]
public class CwSkimmerChannelPlugin : IChannelPlugin
{
    public double FrequencyOffsetHz { get; set; } = 0;
    public double RequiredBandwidthHz => 3000; // 3kHz voldoende voor CW

    public void ProcessSamples(int ddcIndex, ReadOnlySpan<float> iq, int sampleRate)
    {
        // DSP: goertzel filter bank voor CW toon detectie
        // Output: decoded CW callsigns → BroadcastEventAsync("cw-decode", ...)
    }

    public bool TryGetAudioOutput(Span<float> audio, out int n) { n = 0; return false; }
}
```

### 11.4 SDRangel vs Zeus Plugin Model Vergelijking

| Dimensie | SDRangel (Qt/C++) | Zeus (.NET/React) | NovaSdr Doel |
|---------|------------------|------------------|--------------|
| Plugin taal | C++ (Qt) | C# (.NET) + TS | C# + TS |
| Channel concept | Ja (centraal) | Beperkt | Ja (uitbreid Zeus) |
| Freq. offset | Ja per channel | Onbekend | Ja |
| GUI isolatie | Ja (Qt widget) | Ja (React bundle) | Ja |
| Headless mode | Ja | Ja | Ja |
| Community plugins | Actief ecosysteem | v0.1 — nieuw | Doel |
| ABI stabiliteit | Medium (C++ ABI) | Hoog (.NET ABI) | Hoog |
| Plugin sandbox | Nee | Beperkt | Ja (AssemblyLoadContext) |
| Discovery | Handmatig | plugin.json scan | plugin.json scan |

**Conclusie:** NovaSdr combineert het Zeus plugin manifest/lifecycle model met het SDRangel channel plugin concept. Dit geeft de expressiviteit van SDRangel (meerdere decoders op één hardware stream) met de moderniteit van Zeus (.NET 10, async, WebSocket).

---

*Einde bestand 06 — Ham Radio Ecosysteem & Plugin Analyse*
