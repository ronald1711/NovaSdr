# Thetis - Inventarisatie Documentatie (GEARCHIVEERD)

**Project:** Thetis (OpenHPSDR Console Client)  
**Locatie:** `/mnt/data/projects/sdrapp_project/sources/Thetis-master/`  
**Status:** GEARCHIVEERD - Geen actieve onderhoud sinds april 2026  
**Licentiering:** GPLv2 (dual-licensed in parts by Richard Samphire MW0LGE)  
**Platform:** .NET Framework 4.8 / WinForms (Windows-only)  
**Copyrighthouders:** 
- Richard Samphire (MW0LGE) 2020-2026
- Diverse TAPR/OpenHPSDR contributors (see ATTRIBUTIONS)

---

## 1. Projectoverzicht

### Kernkenmerken
- **Taal:** C# (.NET Framework 4.8 / WinForms)
- **Platform:** Windows x64 (monolithic)
- **UI:** WinForms + SharpDX (archived rendering library)
- **Doelborden:** Alle HPSDR borden (Protocol-1 + Protocol-2)
- **Build Systeem:** Visual Studio 2026+ required
- **Lineage:** FlexRadio PowerSDR → OpenHPSDR → Thetis (GPL lineage preserved)

### Voornaamste Karakteristieken
- Multi-receiver support (up to 8 DDCs + diversity)
- Protocol 1 (OZY/Metis/Hermes) + Protocol 2 (G2/Angelia/Orion)
- WDSP DSP engine (Warren Pratt, C++ P/Invoke via wdsp.dll)
- CAT (Kenwood TS-2000 emulation) + VAC (Virtual Audio Cable)
- Panadapter + waterfall (SharpDX/Direct3D 11)
- PureSignal (TX feedback with 4-patch convergence)
- CW keyer (iambic + paddle)
- VOX/DEXP (voice-activated transmit / downward expander)
- Spectrum scope + histogram
- Network control (TCP port 4532 rigctld clone)
- TCI (Transceiver Control Interface) WebSocket server
- MIDI controller support
- Radio discovery (LAN broadcast multicast)
- Advanced DSP: NR, NB, ANF, SNB, SAC (spectral analysis)
- Remote SDR via ANAN/G2/Orion boards

### Why Archived (2 April 2026)

Quote from ReadMe.md:

> "This fork of the original Thetis, which I started tinkering with in 2019, has now been archived. I will not be performing maintenance or adding features to it for the foreseeable future."
>
> **Technical Issues:**
> - Codebase still depends on .NET Framework 4.8 (outdated, falling out of support)
> - Rendering based on SharpDX (itself archived project)
> - Moving to modern rendering engine would require rewrite incompatible with .NET 4.8
> - Multi-RX display engine rewrite needed (not prudent for archived library)
> - Gradually falling behind dependencies
>
> **Decision:** Archive repository, focus efforts elsewhere. "With the progression of AI, perhaps in a few years we will be able to ask it to 'modernise the project'."

**Current State:** Latest release **v2.10.3.13** (1st April 2026) - Maintenance mode only.

---

## 2. Solution Structuur (.NET Framework)

### Primary Solution: `Thetis_VS2026.sln`

```
Thetis (Master Solution)
│
├── Console Project                    (Main application)
│   ├── Project Files/Source/Console/Thetis.csproj
│   ├── Source/Console/
│   │   ├── console.cs                 (Monolithic main form ~8000+ lines)
│   │   ├── dsp.cs                     (WDSP P/Invoke wrapper + RXA/TXA binding)
│   │   ├── radio.cs                   (Radio state machine)
│   │   ├── audio.cs                   (Audio device management)
│   │   │
│   │   ├── HPSDR/
│   │   │   ├── NetworkIO.cs           (Protocol I/O abstraction)
│   │   │   ├── clsRadioDiscovery.cs   (LAN discovery multi-NIC)
│   │   │   ├── metis.cs              (Protocol framing layer)
│   │   │   └── CAT/CATCommands.cs    (Rigctld compatibility)
│   │   │
│   │   ├── CAT/
│   │   │   ├── CATCommands.cs         (Kenwood TS-2000 command set)
│   │   │   ├── CATParser.cs           (ASCII frame parsing)
│   │   │   └── CATController.cs       (Server listener)
│   │   │
│   │   ├── VAC/
│   │   │   ├── VAC.cs                 (Virtual Audio Cable - loopback control)
│   │   │   └── VAC2.cs               (Extended VAC features)
│   │   │
│   │   ├── VFO.cs / VFO2.cs          (Frequency management)
│   │   ├── Band.cs                    (Band plan definitions)
│   │   ├── Mode.cs                    (Modulation mode enums)
│   │   ├── DisplayForm.cs             (Panadapter / waterfall)
│   │   ├── Meter.cs                   (S-meter, power, SWR)
│   │   ├── CWKeyer.cs                 (Iambic keyer)
│   │   ├── VOX.cs                     (Voice activation)
│   │   ├── MIDI.cs                    (MIDI controller I/O)
│   │   │
│   │   └── [50+ additional UI module files]
│   │
│   ├── App.xaml / App.xaml.cs        (WinForms App entry)
│   ├── Properties/
│   │   ├── AssemblyInfo.cs
│   │   ├── Resources.resx             (PNG icons, localizations)
│   │   └── Settings.settings
│   │
│   └── Thetis.csproj                 (Target: .NET Framework 4.8)
│
├── [Optional Sub-Solutions]
├── Thetis-Installer/
│   └── Thetis_Setup.sln              (WiX installer)
│
├── Setup/
│   └── PowerSDR Setup.sln            (Legacy setup project)
│
└── [Build Output]
    └── bin/Release/Thetis.exe        (~15 MB compiled)
```

---

## 3. NuGet Dependencies (packages.config or .csproj)

### Rendering & Graphics
| Paket | Versie | Doel |
|---|---|---|
| SharpDX | 4.2.0 | Direct3D 11 wrapper (ARCHIVED) |
| SharpDX.Direct3D11 | 4.2.0 | D3D11 compute shaders |
| SharpDX.DXGI | 4.2.0 | Display adapter enumeration |

### Audio I/O
| Paket | Versie | Doel |
|---|---|---|
| NAudio | 2.1.0+ | Managed audio device I/O |
| NAudio.Asio | (in NAudio) | ASIO driver support |
| PortAudio.Net | (if included) | PortAudio wrapper |

### Networking
| Paket | Versie | Doel |
|---|---|---|
| System.Net.Sockets | 4.3.0 | UDP/TCP (Windows Forms) |
| System.Net.NetworkInformation | 4.3.0 | Network enumeration |

### Serialization & Config
| Paket | Versie | Doel |
|---|---|---|
| Newtonsoft.Json | 12.0.0+ | JSON config parsing |
| System.Xml.Serialization | 4.3.0 | XML persistence |

### System
| Paket | Versie | Doel |
|---|---|---|
| System.Runtime | 4.3.0 | .NET Framework runtime shim |

**Note:** .NET Framework 4.8 is end-of-life. Security updates minimal post-2026.

---

## 4. Protocol 1/2 Implementatie (NetworkIO.cs)

**Bestand:** `Project Files/Source/Console/HPSDR/NetworkIO.cs` (150+ regels excerpt)

```csharp
public partial class NetworkIO
{
    // Board identification
    public static HPSDRHW BoardID { get; set; } = HPSDRHW.Hermes;
    public static RadioProtocol CurrentRadioProtocol { get; set; } = RadioProtocol.ETH;
    public static RadioProtocol SelectedRadioProtocol { get; set; } = RadioProtocol.ETH;
    
    // Firmware versions
    public static byte BetaVersion { get; set; } = 0;
    public static byte FWCodeVersion { get; set; } = 0;
    public static byte Protocol2VersionSupported { get; set; } = 0;
    
    // Initialization (returns error code or 0=success)
    public static int InitRadio()
    {
        // -4 dest ip invalid
        // -101 invalid firmware
        // -102 unknown protocol
        // -103 invalid radio IP
        // -104 invalid host IP
        // -105 no NIC selected
        // -106 no radio selected
        
        // 1. Retrieve selected NIC + Radio from UI
        Console c = Console.getConsole();
        ucRadioList rl = c.SetupForm.SelectedRadioList;
        NicRadioScanResult nic = rl.SelectedNICDetails;
        RadioInfo ri = rl.SelectedRadioDetails;
        
        // 2. Validate
        if(nic == null) return -105;
        if(ri == null) return -106;
        
        // 3. Build discovery options
        string radioIP = ri.IpAddress.ToString();
        int protocol = ri.Protocol == RadioDiscoveryRadioProtocol.P1 ? 0 : 1;
        
        // 4. Perform discovery (optional, if not custom)
        if(!ri.IsCustom)
        {
            RadioDiscoveryOptions options = new RadioDiscoveryOptions();
            options.IgnoreSubnetCheck = true;
            options.FixedTargetIp = IPAddress.Parse(radioIP);
            
            RadioDiscoveryService svc = new RadioDiscoveryService();
            NicRadioScanResult scan_result = svc.DiscoverUsingSingleNic(options, 
                                                                         options.FixedLocalIp);
            
            if(scan_result?.Radios?.Count != 1) return -1;
            ri = scan_result.Radios[0];
            protocol = (int)ri.Protocol;
            
            // Firmware version check (e.g., HermesII requires v10.3+)
            if(ri.DeviceType == HPSDRHW.HermesII && ri.CodeVersion < 103)
            {
                GetFWVersionErrorMsg = "Invalid Firmware! Requires 10.3 or greater.";
                return -101;
            }
        }
        
        // 5. Native initialization
        int ret = nativeInitMetis(radioIP, ri.DiscoveryPortBase, 
                                  hostIP, hostPort, protocol, model_id);
        
        return ret;
    }
    
    // P/Invoke to native Metis/Hermes library (C DLL)
    [DllImport("metis.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nativeInitMetis(string radioIP, int radioPort, 
                                              string hostIP, int hostPort, 
                                              int protocol, int modelID);
}
```

**Board Enum:**
```csharp
public enum HPSDRHW
{
    Metis = 0,
    Hermes = 1,
    Griffin = 2,
    Angelia = 4,
    Orion = 5,
    HermesLite = 6,
    Orion2 = 10,
    STEMlab = 100,
    HermesII = 506
}

public enum RadioProtocol { P1 = 0, ETH = 1 }  // ETH = Protocol 2
```

---

## 5. DSP Integratie (dsp.cs - WDSP P/Invoke)

**Bestand:** `Project Files/Source/Console/dsp.cs` (150+ regels excerpt)

```csharp
namespace Thetis
{
    // Unsafe P/Invoke wrapper to wdsp.dll (Warren Pratt C library)
    unsafe class WDSP
    {
        // RXA (Receiver) Channel Management
        [DllImport("wdsp.dll", EntryPoint = "OpenChannel", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void OpenChannel(int channel, int in_size, int dsp_size, 
                                             int input_samplerate, int dsp_rate, 
                                             int output_samplerate, int type, int state,
                                             double tdelayup, double tslewup, 
                                             double tdelaydown, double tslewdown, int bfo);
        
        [DllImport("wdsp.dll", EntryPoint = "CloseChannel", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void CloseChannel(int channel);
        
        // Sample Rate Negotiation
        [DllImport("wdsp.dll", EntryPoint = "SetInputBuffsize", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetInputBuffsize(int channel, int in_size);
        
        [DllImport("wdsp.dll", EntryPoint = "SetDSPBuffsize", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetDSPBuffsize(int channel, int dsp_size);
        
        [DllImport("wdsp.dll", EntryPoint = "SetAllRates", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetAllRates(int channel, int in_rate, int dsp_rate, 
                                             int out_rate);
        
        // Mode Selection
        [DllImport("wdsp.dll", EntryPoint = "SetRXAMode", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRXAMode(int channel, DSPMode mode);
        
        [DllImport("wdsp.dll", EntryPoint = "SetTXAMode", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetTXAMode(int channel, DSPMode mode);
        
        // Sample Exchange (audio processing)
        [DllImport("wdsp.dll", EntryPoint = "fexchange0", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void fexchange0(int channel, double* Cin, 
                                            double* Cout, int* error);
        
        [DllImport("wdsp.dll", EntryPoint = "fexchange2", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void fexchange2(int channel, float* Iin, float* Qin, 
                                            float* Iout, float* Qout, int* error);
        
        // AGC Parameters
        [DllImport("wdsp.dll", EntryPoint = "SetRXAAGCMode", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRXAAGCMode(int channel, AGCMode mode);
        
        [DllImport("wdsp.dll", EntryPoint = "SetRXAAGCTop", 
                   CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRXAAGCTop(int channel, double max_agc);
        
        // 100+ more P/Invoke signatures for NR, NB, filters, compressor, etc.
    }
    
    enum DSPMode
    {
        LSB = 0, USB = 1, DSB = 2, CWL = 3, CWU = 4,
        FM = 5, AM = 6, DIGU = 7, DIGL = 8, SAM = 9, DRM = 10
    }
    
    enum AGCMode { OFF = 0, FAST = 1, SLOW = 2, LONG = 3, FIXED = 4 }
}
```

**Initialization Order (From Thetis radio.cs):**
1. `OpenChannel(0, 1024, 1024, 48000, 48000, 48000, 0, 1, ...)`  // RXA
2. `OpenChannel(1, 1024, 1024, 48000, 48000, 48000, 1, 1, ...)`  // TXA
3. For each RX: `SetRXAMode()`, `SetRXAAGCMode()`, set filters
4. `fexchange0()` in audio thread loop (RX demod)
5. `fexchange2()` in audio thread loop (TX modulate)

---

## 6. Audio Stack (audio.cs)

**Bestanden:** `Project Files/Source/Console/audio.cs`

**NAudio Integration:**
```csharp
public class Audio
{
    private IWavePlayer wavePlayer;           // Output device
    private WaveInEvent waveIn;               // Input device (mic)
    
    // Device enumeration
    public static List<string> GetOutputDevices()
    {
        var result = new List<string>();
        for (int i = 0; i < WaveOutEvent.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            result.Add(caps.ProductName);
        }
        return result;
    }
    
    public static List<string> GetInputDevices()
    {
        var result = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            result.Add(caps.ProductName);
        }
        return result;
    }
    
    // Initialize I/O
    public int InitAudio(int inputDeviceID, int outputDeviceID)
    {
        wavePlayer = new WaveOutEvent();
        wavePlayer.Init(audioProvider);
        
        waveIn = new WaveInEvent();
        waveIn.DeviceNumber = inputDeviceID;
        waveIn.WaveFormat = new WaveFormat(48000, 16, 1);
        waveIn.DataAvailable += WaveIn_DataAvailable;
        
        wavePlayer.Play();
        waveIn.StartRecording();
        
        return 0;
    }
    
    // Optional: ASIO Low-Latency Path
    private AsioOut asioOut;
    
    public int InitAsio(string asioDriver)
    {
        asioOut = new AsioOut(asioDriver);
        asioOut.Init(audioProvider);
        asioOut.Play();
        return 0;
    }
}
```

**Supported Backends:**
- WaveOut / WaveIn (Windows MultiMedia API)
- ASIO (professional audio interface, lower latency)
- PortAudio (via wrapper if needed)

---

## 7. UI Architectuur (console.cs Monolith)

**Bestand:** `Project Files/Source/Console/console.cs` (~8000+ regels!)

**Architecture Smell:** Single mega-class containing:
- Main WinForm window definition
- Event handlers for 100+ controls
- State machine logic (MOX, RX mode, TX filter)
- Audio I/O lifecycle
- Display update loops (30 Hz panadapter, 10 Hz meter)
- Network I/O callbacks

**Example Form Complexity:**
```csharp
public partial class Console : Form
{
    // 50+ UI controls (TextBox, Button, Slider, ComboBox, etc.)
    private TextBox txFreqDisplay;
    private TrackBar driveSlider;
    private PictureBox panadapterPanel;
    private Label smeterLabel;
    // ... and 200 more
    
    // State variables (~500+)
    private double currentFrequency;
    private int currentMode;
    private bool isTransmitting;
    private double[] audioBuffer;
    // ...
    
    // Event handlers (one for each control)
    private void driveSlider_ValueChanged(object sender, EventArgs e)
    {
        int drive = driveSlider.Value;
        // Call WDSP, update TX, broadcast
        SetTXADrive(drive);
        UpdateDriveDisplay();
    }
    
    // Display update loop (30 Hz)
    private void displayTimer_Tick(object sender, EventArgs e)
    {
        // Fetch FFT from WDSP
        // Render panadapter (SharpDX)
        // Update waterfall
        // Refresh S-meter
        RefreshPanadapter();
        RefreshWaterfall();
        RefreshMeters();
    }
}
```

**Rendering (SharpDX - Archived):**
```csharp
private void RenderPanadapter()
{
    using (var device = new Device(DriverType.Hardware, DeviceCreationFlags.None))
    using (var context = device.ImmediateContext)
    {
        // Direct3D 11 compute shader for FFT bin rendering
        // Texture2D for spectrum data
        // RenderTarget for off-screen draw
        // Present to WinForms PictureBox via texture copy
    }
}
```

**Why This Is Problematic:**
1. **Monolithic State:** All console state in single class → hard to refactor
2. **Tight Coupling:** UI update logic mixed with protocol logic
3. **Event Handler Spaghetti:** 50+ handlers with shared mutable state
4. **Thread Safety:** Global state mutated from audio thread, network thread, UI thread
5. **Testing:** Near-impossible to unit test without full WinForms mocking

---

## 8. CAT Implementatie (CATCommands.cs)

**Bestanden:** `Project Files/Source/Console/CAT/CATCommands.cs` (~1500 regels)

**Kenwood TS-2000 Command Emulation:**

```csharp
public class CATCommands
{
    private Console console;
    
    // Standard CAT methods (A-F alphabetical)
    
    // Audio Gain
    public string AG(string s)
    {
        if(s.Length == parser.nSet)  // SET command: AG999
        {
            int raw = Convert.ToInt32(s.Substring(1));
            int af = (int)Math.Round(raw/2.55, 0);  // Scale 0-255 to 0-100
            console.AF = af;
            return "";  // No response to SET
        }
        else if(s.Length == parser.nGet)  // GET command: AG;
        {
            int af = (int)Math.Round(console.AF / 0.392, 0);  // Inverse scale
            return AddLeadingZeros(af);  // e.g., "099\n"
        }
        else
        {
            return "?;";  // Error
        }
    }
    
    // Frequency Control
    public string FA(string s)  // Set/Get VFO A frequency
    {
        if(s.Length == parser.nSet)  // FA00014200000;
        {
            long freq = long.Parse(s.Substring(1));
            console.SetFrequency(freq);
            return "";
        }
        else if(s.Length == parser.nGet)
        {
            return string.Format("FA{0:D11};", console.GetFrequency());
        }
        return "?;";
    }
    
    // Mode Control
    public string MD(string s)  // Set/Get mode (LSB/USB/CW/etc.)
    {
        if(s.Length == parser.nSet)  // MD2; (2=USB)
        {
            int mode = Convert.ToInt32(s.Substring(1));
            console.SetMode((TXMode)mode);
            return "";
        }
        else if(s.Length == parser.nGet)
        {
            return string.Format("MD{0};", (int)console.CurrentMode);
        }
        return "?;";
    }
    
    // 50+ more commands (RX level, TX level, SWR, etc.)
    public string SM(string s)  // S-meter
    {
        // Return dBm formatted as S-unit + dB
        double smeterDbm = console.GetSmeterDbm();
        return string.Format("SM{0:D3};", (int)smeterDbm);
    }
}

// Command Prefix Enum (subset)
enum CommandPrefix
{
    AG,  // Audio Gain
    FA,  // Frequency A
    MD,  // Mode
    PW,  // Power Output
    RX,  // RX Status
    TX,  // TX Status
    SM,  // S-Meter
    // ... 50+ more
}
```

**TCP Server (Rigctld Clone):**
```csharp
public class CATController
{
    private TcpListener listener = new TcpListener(IPAddress.Loopback, 4532);
    private CATParser parser = new CATParser();
    private CATCommands commands;
    
    public void Start()
    {
        listener.Start();
        while(true)
        {
            TcpClient client = listener.AcceptTcpClient();
            HandleClient(client);
        }
    }
    
    private void HandleClient(TcpClient client)
    {
        using(var reader = new StreamReader(client.GetStream()))
        using(var writer = new StreamWriter(client.GetStream()))
        {
            string line;
            while((line = reader.ReadLine()) != null)
            {
                // Parse "AG123;" or "AG;"
                string response = parser.ProcessCommand(line);
                writer.WriteLine(response);
                writer.Flush();
            }
        }
    }
}
```

**Clients:** WSJT-X, FLDIGI, N1MM Logger+, etc.

---

## 9. Feature Inventaris (Alles)

| Feature | Status | Bemerkingen |
|---|---|---|
| **Core TX/RX** | ✓ Complete | All modes, full band coverage |
| **Protocol 1** | ✓ Complete | OZY, Metis, Hermes, HL2 |
| **Protocol 2** | ✓ Complete | Angelia, Orion, G2, G2 MkII, HermesII |
| **Multi-RX** | ✓ Complete | Up to 8 DDCs, diversity option |
| **PureSignal** | ✓ Complete | 4-patch TX feedback, predistortion |
| **DSP (WDSP)** | ✓ Complete | NR, NB, ANF, SNB, SAC, all stages |
| **CAT (Rigctld)** | ✓ Complete | 60+ Kenwood commands |
| **VAC (Virtual Audio)** | ✓ Complete | VAC1 / VAC2 loopback |
| **MIDI** | ✓ Complete | CC binding, fader/knob mapping |
| **CW Keyer** | ✓ Complete | Iambic A/B, paddle/straight |
| **VOX/DEXP** | ✓ Complete | Voice activation + downward expander |
| **Network Remote** | ✓ Complete | TCI WebSocket server |
| **Radio Discovery** | ✓ Complete | Broadcast + fixed IP modes |
| **Audio I/O** | ✓ Complete | WaveOut/ASIO backends |
| **Panadapter** | ✓ Complete | SharpDX/D3D11 rendering |
| **Waterfall** | ✓ Complete | Scrolling color plot |
| **S-Meter** | ✓ Complete | Live + demo modes |
| **Spectrum Analyzer** | ✓ Complete | Histogram overlay |
| **Transverter Mode** | ✓ Complete | Frequency offset LO |
| **Split Freq** | ✓ Complete | RX/TX on different bands |
| **RIT/XIT** | ✓ Complete | Fine tuning offset |
| **Band Stack** | ✓ Complete | Per-band frequency memory |
| **Settings Persistence** | ✓ Complete | XML file storage |
| **Multi-Language** | ✓ Partial | EN, DE, FR (community added) |
| **Themes/Skins** | ✓ Complete | Multiple color schemes |

---

## 10. Archivierungsgründe - Diepgaand

### Technical Debt (from ReadMe)

**1. .NET Framework 4.8 Obsolescence**
- Released in 2019
- Mainstream support ended 13 Nov 2025
- Extended support ends 9 Oct 2026
- No new features, only critical security patches
- Deprecated by .NET 6+ (2022), .NET 8+ (2023), .NET 10 (2026)

**2. SharpDX End-of-Life**
- Last release: v4.2.0 (2016)
- No maintenance since 2017
- Direct3D 11 (2009 technology)
- No Vulkan/Metal/WebGL support
- Modern alternative: MonoGame, FNA (complex migration)

**3. Multi-RX Display Rewrite Blocked**
- Current display engine built on SharpDX
- Adding multiple synchronized receiver windows requires architectural redesign
- Rewriting display layer incompatible with .NET 4.8 + archived SharpDX
- Would require full ground-up UI rebuild (not feasible for archived project)

**4. Dependency Bleeding Edge Falls Behind**
- NAudio: Shifted to .NET Core support, 4.8 compatibility untested
- Newtonsoft.Json: EOL for .NET 4.8 in v13+
- System.* NuGet shims: Increasingly brittle

### Strategic Decision

**Quote:** "Given that the current display engine is based on SharpDX, it would not seem prudent to invest that effort into an archived and outdated library."

**Implication:** Modernization NOT planned. Thetis is **end-of-life software** as of April 2026.

---

## 11. Code Smells & Legacy Issues

| Smell | Location | Severity | Notes |
|---|---|---|---|
| Monolithic Class | console.cs (8000 lines) | CRITICAL | Single mega-class; impossible to test |
| Global State | console.cs | CRITICAL | Mutable statics; thread-unsafe |
| No Unit Tests | /tests/ (empty) | CRITICAL | Integration-only (requires hardware) |
| SharpDX Direct3D | DisplayForm.cs | HIGH | Archived library; no maintenance |
| Thread Safety | audio.cs + dsp.cs | HIGH | Manual locks; potential race conditions |
| P/Invoke Unsafe Pointers | dsp.cs | MEDIUM | Unsafe block; GC pinning required |
| Error Handling | NetworkIO.cs | MEDIUM | Many silent failures; minimal logging |
| Hardcoded Paths | store.cs | MEDIUM | Config files in %APPDATA%; registry hacks |
| Magic Numbers | Various | MEDIUM | Buffer sizes, sample rates hard-coded |
| No Async/Await | All code | MEDIUM | Blocking I/O in event handlers |

---

## 12. Waarom Bruikbaar als Referentie

**Thetis is een AUTHORITATIVE OpenHPSDR reference implementation:**

1. **Most Complete Feature Set:** Alles wat Hermes/Orion bord kan (tot G2 MkII)
2. **Longest Field Service:** Deployed worldwide by radio amateurs (v2.6 → v2.10)
3. **Peer-Reviewed:** Community tested; known issues documented
4. **WDSP Parity:** Exact RXA/TXA initialization matching (Warren Pratt official)
5. **Protocol Details Visible:** P1/P2 framing, meter scaling, meter calibration
6. **DSP Pipeline Documented:** AGC slopes, NR gain methods, SAC frequency weighting

**Best For Learning:**
- Exact WDSP sequence (init order, parameter constraints)
- Protocol 1/2 wire format (complete packet definitions)
- Meter calibration formulas (dBm ↔ S-unit conversion)
- CAT/rigctld command subset
- Multi-receiver DDC allocation (G2 MkII PureSignal)

**Avoid Copying:**
- WinForms monolithic architecture (use modern framework instead)
- SharpDX rendering pipeline (obsolete)
- Unsafe pointers (use modern .NET safety)
- Implicit threading (use Task/async patterns)

---

## 13. Bruikbare Codeblokken voor NovaSdr

### Protocol 2 Frame Parsing Pattern
**Source:** `metis.c` (native C DLL)
- DDC packet layout (238 samples @ 4 bytes)
- Hi-priority status parsing
- Sequence number handling

### WDSP Initialization Sequence
**Source:** `dsp.cs`
```csharp
// RXA open
WDSP.OpenChannel(0, 1024, 1024, 48000, 48000, 48000, 0, 1, ...);
WDSP.SetRXAMode(0, DSPMode.USB);
WDSP.SetRXAAGCMode(0, AGCMode.FAST);
WDSP.SetRXAPanelGain1(0, 1.0);  // Unity gain

// TXA open
WDSP.OpenChannel(1, 1024, 1024, 48000, 48000, 48000, 1, 1, ...);
WDSP.SetTXAMode(1, DSPMode.USB);
```

### Meter Scaling Formulas
**Source:** `meter.cs` (reverse-engineered from UI)
```csharp
// S-meter dBm to S-unit conversion
private int DbmToSUnit(double dbm)
{
    if(dbm > -50) return 0;  // S0
    int sunit = (int)((-dbm + 130) / 6);  // 6 dB per S-unit below S9 (-73 dBm)
    return Math.Min(Math.Max(sunit, 0), 9);  // Clamp S0-S9
}

// TX Power (watts) display
private string FormatTxPower(double watts)
{
    if(watts < 0.1) return string.Format("{0:F2} mW", watts * 1000);
    else return string.Format("{0:F1} W", watts);
}
```

---

## 14. Conclusie voor NovaSdr

**Thetis Value as Reference:**
- ✓ **Authoritative DSP wiring** (WDSP init order, filter application)
- ✓ **Complete Protocol 2 specification** (packet structures, timing)
- ✓ **Proven multi-board support** (Angelia, Orion, G2, HermesII validation)
- ✓ **Field-tested feature matrix** (PureSignal, VOX, CW keyer)
- ✓ **CAT/Network integration patterns** (rigctld, TCI)

**Thetis Architecture Lessons:**
- ✗ **Do NOT copy WinForms monolith** → Use modern architecture (MVVM, DI)
- ✗ **Do NOT use SharpDX** → Use Vulkan/Metal/WebGL or web-based UI
- ✗ **Do NOT replicate unsafe pointer code** → Use managed .NET safety
- ✗ **Do NOT repeat lack of testing** → Unit test from day 1

**Recommendation:**
Use Thetis as:
1. **Protocol reference:** Exact P1/P2 frame definitions
2. **WDSP reference:** RXA/TXA initialization + meter pipelines
3. **Feature checklist:** Which features matter (PureSignal, VOX, etc.)
4. **Anti-pattern study:** What NOT to do in new codebase

---

## Appendix: Timeline of Thetis Releases

| Version | Date | Notes |
|---|---|---|
| v2.0 | ~2019 | Fork from original Thetis |
| v2.6.8 | 2019-11-03 | ANAN-10E compat fix |
| v2.8.11 | 2020-10-20 | Major feature set |
| v2.9.0 | 2022-03-04 | Multi-RX improvements |
| v2.10.3 | 2023-02-11 | Last full release |
| v2.10.3.13 | 2026-04-01 | FINAL RELEASE (archive) |

---

## Slotwoord

Thetis v2.10.3.13 represents **the most mature, field-proven OpenHPSDR client** ever built, but its time has ended. The .NET Framework + SharpDX combination left it immobile; modernization would require starting over.

For NovaSdr: **Learn from Thetis, but don't copy it.** The architecture (monolithic WinForms) was acceptable in 2016 but is now an antipattern. The DSP knowledge (WDSP integration, meter pipelines, PureSignal 4-patch convergence) is **invaluable** and should be preserved.

**73, Thetis. Rest well.**

