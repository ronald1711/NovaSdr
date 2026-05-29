# NovaSdr — Fase 8: Multi-Device en RX2 Architectuur
*Evidence-based ontwerp | Gegenereerd: 2026-05-29*

---

## Overzicht

Dit document beschrijft de architectuur voor het ondersteunen van meerdere SDR-devices tegelijkertijd in NovaSdr — specifiek het model waarbij een primaire transceiver (Brick2) wordt gecombineerd met één of meerdere auxiliary receive-only devices.

---

## 8.1 Use Cases

### Primaire use cases

| Configuratie | Primary | Auxiliary RX | Toepassing |
|---|---|---|---|
| **HF + Monitor** | Brick2 P2 (14 MHz QSO) | RTL-SDR (band monitor) | Continu band-overzicht naast QSO |
| **HF Diversity** | Brick2 P2 (DualRx DDC0/DDC1) | — | Intern P2 diversity, geen extra device |
| **HF + VHF** | Brick2 P2 (HF) | SDRplay RSPdx (VHF) | Multi-band bewaking |
| **QRO + Monitor** | Brick2 P2 (TX) | RTL-SDR (TX monitor) | TX spectrum monitoring tijdens operatie |
| **Full VHF-UHF** | PlutoPlus (VHF/UHF TX+RX) | SDRplay RSPdx (HF) | Reverse setup: PlutoPlus als primary |
| **Toekomst: PS** | Brick2 P2 (TX) | PlutoPlus (loopback) | PureSignal alternatief via PlutoPlus |

### Secundaire use cases (toekomst)
- WSPR/FT8 monitoring op RX2 terwijl QSO op primary
- Satelliettransceiver: PlutoPlus als UHF TX/RX + Brick2 als HF backup
- Contest: 2 banden tegelijk monitoren

---

## 8.2 RadioSession Model

```csharp
/// <summary>
/// Vertegenwoordigt een complete radio-sessie met één primary transceiver
/// en nul of meer auxiliary receivers.
/// </summary>
public sealed class RadioSession : IAsyncDisposable
{
    /// <summary>
    /// De primaire TX+RX transceiver (bijv. Brick2 P2).
    /// Mag niet null zijn — sessie vereist altijd een primary device.
    /// </summary>
    public ITransceiver PrimaryTransceiver { get; }

    /// <summary>
    /// Lijst van auxiliary RX-only devices (bijv. SDRplay, RTL-SDR).
    /// Kan leeg zijn.
    /// </summary>
    public IReadOnlyList<AuxiliaryReceiver> AuxiliaryReceivers { get; }

    /// <summary>
    /// Sessie-brede PTT state — DeviceCoordinator bewaakt dit.
    /// </summary>
    public bool IsMoxActive { get; }

    // Factory methods
    public static RadioSession Create(ITransceiver primary);
    public Task AddAuxiliaryAsync(IDeviceSource device, AuxReceiverConfig config);
    public Task RemoveAuxiliaryAsync(string deviceId);
}

public sealed class AuxiliaryReceiver
{
    public IDeviceSource Device { get; }
    public VfoState Vfo { get; set; }
    public FreqSyncPolicy FreqSync { get; set; }
    public AudioRoutePolicy AudioRoute { get; set; }
    public bool IsEnabled { get; set; }
    public int WdspChannelId { get; }  // WDSP channel (bijv. 16-31 voor aux range)
}
```

---

## 8.3 DeviceCoordinatorService

De `DeviceCoordinatorService` is de centrale orchestrator voor multi-device conflictpreventie.

```csharp
/// <summary>
/// Bewaakt de RadioSession en enforceert policies:
/// - PTT lockout (geen simultane TX op meerdere devices)
/// - Frequentiesynchronisatie (RX2 volgt primary VFO indien policy=FollowPrimary)
/// - Audio routing arbitratie
/// </summary>
public sealed class DeviceCoordinatorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Abonneer op primary MOX state changes
        _primaryTransceiver.MoxChanged += OnPrimaryMoxChanged;

        // Abonneer op VFO changes voor freq-sync
        _radioService.VfoAChanged += OnPrimaryVfoChanged;

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async void OnPrimaryMoxChanged(bool moxOn)
    {
        if (moxOn)
        {
            // PTT LOCKOUT: disable TX op alle auxiliary ITransceiver devices
            foreach (var aux in _session.AuxiliaryReceivers)
            {
                if (aux.Device is ITransceiver auxTx)
                    await auxTx.SetMoxAsync(false, CancellationToken.None);
            }

            // Optioneel: mute RX2 audio tijdens TX (operator keuze)
            if (_settings.MuteRx2DuringTx)
                _audioRouter.SetAuxMute(true);
        }
        else
        {
            if (_settings.MuteRx2DuringTx)
                _audioRouter.SetAuxMute(false);
        }
    }

    private async void OnPrimaryVfoChanged(long newFrequencyHz)
    {
        foreach (var aux in _session.AuxiliaryReceivers)
        {
            switch (aux.FreqSync)
            {
                case FreqSyncPolicy.FollowPrimary:
                    await aux.Device.SetFrequencyAsync(newFrequencyHz, CancellationToken.None);
                    break;

                case FreqSyncPolicy.FollowPrimaryWithOffset:
                    await aux.Device.SetFrequencyAsync(
                        newFrequencyHz + aux.SyncOffsetHz,
                        CancellationToken.None);
                    break;

                case FreqSyncPolicy.Independent:
                default:
                    break; // RX2 VFO onafhankelijk
            }
        }
    }
}
```

---

## 8.4 Frequentiesynchronisatie

### FreqSyncPolicy

```csharp
public enum FreqSyncPolicy
{
    /// <summary>
    /// RX2 heeft een volledig onafhankelijke VFO.
    /// Typisch gebruik: RX2 monitort een andere band.
    /// </summary>
    Independent,

    /// <summary>
    /// RX2 volgt primary VFO-A exact.
    /// Typisch gebruik: Diversity receive met vergelijkbaar device.
    /// NB: echte diversity vereist phase-coherente timing — dat is NIET
    /// gegarandeerd bij verschillende devices.
    /// </summary>
    FollowPrimary,

    /// <summary>
    /// RX2 = primary VFO + vaste offset.
    /// Typisch gebruik: satelliettransceiver (uplink/downlink split),
    /// of transverter (primary = IF, RX2 = RF).
    /// </summary>
    FollowPrimaryWithOffset,
}
```

### Implementatie frequentiesync

Frequentiesync is **software-only** — geen hardware clock-koppeling. Dit betekent:

- **Latency verschil:** Primary Brick2 P2 reageert in < 1ms; RTL-SDR kan 50-100ms achterliggen
- **Geen echte diversity:** Fase-coherente diversity vereist een gemeenschappelijke LO/klok. Dit is architectureel NIET haalbaar met verschillende device-typen.
- **Praktisch gebruik:** Frequentiesync is nuttig voor "volg dezelfde band" monitoring, niet voor fase-coherente combinatie.

**Documenteer als beperking in UI:**
```
"RX2 Freq Sync: RX2 volgt primary VFO (niet phase-coherent).
Geschikt voor band monitoring en frequentie-tracking.
Niet geschikt voor diversity receive."
```

---

## 8.5 Sample Rate Bridge

Elk device levert IQ bij een eigen sample rate. WDSP verwacht 48 kHz IQ.

```
Device               Native rate      Decimatie factor    WDSP input
──────────────────   ─────────────    ──────────────────  ──────────
Brick2 P1/P2         48 kHz           1 (passthrough)     48 kHz ✓
SDRplay RSPdx        250 kHz – 10 MHz 5.2× – 208×         48 kHz
RTL-SDR              2.048 MHz        42.67×              48 kHz
PlutoSDR             2.5 MHz          52.08×              48 kHz
PlutoPlus            2.5 – 61.44 MHz  52× – 1280×         48 kHz
```

### SampleRateBridge klasse

```csharp
/// <summary>
/// Decimeert IQ data van native device sample rate naar target rate (48 kHz).
/// Gebruikt libsamplerate (LGPL) voor hoge kwaliteit decimatie.
/// </summary>
public sealed class SampleRateBridge : IDisposable
{
    private readonly int _inputRate;
    private readonly int _outputRate; // 48000

    public SampleRateBridge(int inputRateHz, int outputRateHz = 48_000)
    {
        _inputRate = inputRateHz;
        _outputRate = outputRateHz;
        // Initialiseer libsamplerate converter (SRC_SINC_BEST_QUALITY)
    }

    /// <summary>
    /// Converteert een IQ-blok van native rate naar 48 kHz.
    /// Retourneert nieuwe IqBlock bij 48 kHz.
    /// </summary>
    public IqBlock? Process(IqBlock input)
    {
        // libsamplerate src_process() of
        // interne polyphase filter als verhouding rationaal is
        // Output: 1024 samples @ 48 kHz voor WDSP FeedIq()
    }
}
```

**Alternatief voor rationele ratios:**
- RTL-SDR 2.048 MHz → 48 kHz: ratio = 2048/48 = 42.667 (irrationeel)
- Gebruik polyphase FIR filter (factor 3/128): 2048 kHz × 3 = 6144 kHz / 128 = 48 kHz ✓
- SDRplay 250 kHz → 48 kHz: factor 250/48 ≈ 5.208 → polyphase 25/24 × 1/5 keten

---

## 8.6 WDSP Channel ID Partitionering

WDSP ondersteunt MAX_CHANNELS=32 (aantoonbaar in WDSP broncode). Bij twee WdspDspEngine instances in één process kunnen channel IDs clashingen.

### Partitionering strategie

```
Primary WdspDspEngine:
  Channel  0 = RX VFO-A (main receive)
  Channel  1 = RX VFO-B (dual watch / subreceiver)
  Channel 14 = TX chain (één per session)
  Channels 2-13 = gereserveerd voor toekomstige DDC uitbreiding

Auxiliary WdspDspEngine (per aux device):
  Channel 16 = Aux RX device 0 (bijv. SDRplay)
  Channel 17 = Aux RX device 1 (bijv. RTL-SDR)
  Channels 18-31 = gereserveerd
```

**Implementatie:**
```csharp
// WdspChannelAllocator.cs
public sealed class WdspChannelAllocator
{
    private const int PrimaryRxStart = 0;
    private const int PrimaryRxCount = 8;
    private const int PrimaryTxChannel = 14;
    private const int AuxRxStart = 16;
    private const int AuxRxCount = 14;

    public int AllocatePrimaryRxChannel() => /* volgende vrije in 0-13 */
    public int AllocateAuxRxChannel() => /* volgende vrije in 16-29 */
    public void ReleaseChannel(int id) => /* vrijgeven */
}
```

---

## 8.7 Audio Routing

### AudioRoutePolicy

```csharp
public enum AudioRoutePolicy
{
    /// <summary>Stuur RX2 audio naar linker kanaal van stereo output.</summary>
    LeftChannel,

    /// <summary>Stuur RX2 audio naar rechter kanaal van stereo output.</summary>
    RightChannel,

    /// <summary>Stuur RX2 audio naar mono mix (samen met primary).</summary>
    MonoMix,

    /// <summary>Schakel RX2 audio uit (spectrum display only).</summary>
    Mute,
}
```

### IAudioRouter interface

```csharp
public interface IAudioRouter
{
    void SetPrimaryRoute(AudioOutputDevice device, AudioChannel channel);
    void SetAuxRoute(int auxIndex, AudioRoutePolicy policy);
    void SetMixBalance(float primaryGain, float auxGain);
    void SetAuxMute(bool muted);  // gebruikt door DeviceCoordinator bij TX
}
```

### Typische operator configuraties

| Scenario | Primary | RX2 | Routing |
|---|---|---|---|
| Solo monitoring | linker+rechter | muted | Stereo primary |
| Dual band | linker+rechter | rechts | Primary stereo + RX2 rechts overlay |
| Diversity check | links | rechts | Primary L, RX2 R |
| Contest bewaking | linker+rechter | muted (spectrum only) | Primary + RX2 visueel |

---

## 8.8 Latency Mismatch

Verschillende devices hebben verschillende end-to-end latency:

| Device | Network latency | WDSP buffer | Audio buffer | Totaal |
|---|---|---|---|---|
| Brick2 P2 | < 1 ms (LAN UDP) | 21.3 ms (1024@48k) | 5-10 ms | **~27-32 ms** |
| Brick2 P1 | < 1 ms (LAN UDP) | 21.3 ms | 5-10 ms | **~27-32 ms** |
| SDRplay | 1-5 ms (USB/API) | 21.3 ms + bridge | 5-10 ms | **~30-40 ms** |
| RTL-SDR | 2-10 ms (USB) | 64 ms (librtlsdr default) + bridge | 5-10 ms | **~75-90 ms** |
| PlutoSDR | 5-15 ms (Ethernet) | 21.3 ms + bridge | 5-10 ms | **~40-50 ms** |

**Conclusie:** Synchrone audio is niet haalbaar tussen primary Brick2 en RTL-SDR (64ms gap).

**Architectuurbeslissing:**
> RX2 audio wordt NIET gesynchroniseerd met primary audio.  
> RX2 is een **second listen window**, niet een diversity combiner.  
> Dit is consistent met hoe professionele operators meerdere receivers gebruiken.

---

## 8.9 Multi-Device UI Aanpak

### Layout principes

```
┌──────────────────────────────────────────────────────────────────┐
│  NovaSdr — Multi-Device Layout                                   │
├──────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────┐ ┌────────────────────────┐  │
│  │  PRIMARY SPECTRUM (Brick2 P2)   │ │  RX2 SPECTRUM          │  │
│  │  14.200 MHz  USB                │ │  (SDRplay RSPdx)       │  │
│  │  [WebGL panadapter + waterfall] │ │  [WebGL panadapter]    │  │
│  └─────────────────────────────────┘ └────────────────────────┘  │
│  ┌─────────────────────────────────┐ ┌────────────────────────┐  │
│  │  VFO-A controls                 │ │  RX2 VFO controls      │  │
│  │  [Freq, Mode, Filter, AGC, etc] │ │  [Freq, Mode, Gain]    │  │
│  └─────────────────────────────────┘ └────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │  S-METER (primary) | TX METERS | RX2 S-METER               │ │
│  └──────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

### React component structuur

```typescript
// Nieuw panel: MultiDevicePanel.tsx
const MultiDevicePanel: React.FC = () => {
  const primaryDevice = useDeviceStore(s => s.primaryDevice);
  const auxDevices = useDeviceStore(s => s.auxiliaryDevices);

  return (
    <div className="flex gap-2">
      {/* Primary transceiver panel */}
      <PrimaryReceiverPanel deviceId={primaryDevice.id} />

      {/* Auxiliary receiver panels */}
      {auxDevices.map(aux => (
        <AuxiliaryReceiverPanel
          key={aux.id}
          deviceId={aux.id}
          freqSync={aux.freqSyncPolicy}
          audioRoute={aux.audioRoute}
        />
      ))}

      {/* Add device button */}
      <AddAuxDeviceButton />
    </div>
  );
};

// DeviceManagerPanel.tsx - device discovery en koppeling
const DeviceManagerPanel: React.FC = () => {
  const discoveredDevices = useDiscoveryStore(s => s.discovered);

  return (
    <div>
      <h2>Beschikbare Devices</h2>
      {discoveredDevices.map(dev => (
        <DeviceCard
          key={dev.id}
          device={dev}
          onAttachAsPrimary={() => dispatch(attachPrimary(dev.id))}
          onAttachAsAux={() => dispatch(attachAux(dev.id))}
        />
      ))}
    </div>
  );
};
```

---

## 8.10 Conflictpreventie en Safety

### Regels (afdwingbaar door DeviceCoordinatorService)

| Situatie | Actie | Ernst |
|---|---|---|
| Primary MOX aan + aux device heeft TX-capability | Aux TX uitschakelen | **Critical** — directe implementatie |
| Twee devices op zelfde frequentie + TX | Waarschuwing in UI | **High** — visuele feedback |
| RX2 sample rate te hoog voor decimatie | Automatisch aanpassen + loggen | **Medium** |
| RX2 frequentie buiten device bereik | Foutmelding + freq correctie | **Medium** |
| WDSP channel ID uitgeput (> 32) | Weiger toevoeging nieuw aux device | **High** |

### PTT hardware safety

Bij hardwired PTT (via seriele poort of CAT):
```csharp
// CATPlugin.cs
void OnPttActivated()
{
    // Notificeer DeviceCoordinatorService VOOR MOX activering
    _coordinator.NotifyHardwarePttActivated();
    // Coordinator voert lockout uit op alle aux ITransceiver devices
    // Dan pas: _primaryTransceiver.SetMoxAsync(true)
}
```

---

## 8.11 Profielen per Stationconfiguratie

```csharp
public sealed record StationProfile
{
    public string Id { get; init; }
    public string Name { get; init; }  // bijv. "HF Contest Station"

    // Primary device configuratie
    public DeviceProfileRef PrimaryDevice { get; init; }

    // Auxiliary devices
    public IReadOnlyList<AuxiliaryDeviceProfileRef> AuxDevices { get; init; }

    // Saved VFO states per band
    public IReadOnlyDictionary<HamBand, BandStackEntry> BandStack { get; init; }

    // Multi-device audio routing policy
    public AudioRoutingProfile AudioRouting { get; init; }
}
```

Profielen opgeslagen in LiteDB `StationProfileStore` (uitbreiding van bestaand Zeus LiteDB model).

---

## 8.12 Realistische DeviceCombinaties

| Combinatie | Haalbaarheid | Kwaliteitsrisico | Opmerkingen |
|---|---|---|---|
| Brick2 P2 + RTL-SDR | ✓✓ Haalbaar | Laag | Eenvoudigste configuratie |
| Brick2 P2 + SDRplay RSPdx | ✓✓ Haalbaar | Laag | Uitstekende kwaliteit |
| Brick2 P2 + PlutoPlus | ✓ Haalbaar | Midden | Libiio latency meting nodig |
| Brick2 P2 + 2× RTL-SDR | ✓ Haalbaar | Laag | USB bandwidth bewaken |
| PlutoPlus als primary + SDRplay als HF RX2 | ✓ Haalbaar | Midden | Ongewone setup |
| Brick2 P2 + PlutoPlus als PS feedback | Toekomst | Hoog | Fase 3, vereist timing onderzoek |
| 3+ devices simultaan | Experimenteel | Hoog | CPU load en audio complexiteit |
