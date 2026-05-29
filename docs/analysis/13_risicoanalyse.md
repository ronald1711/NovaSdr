# NovaSdr — Fase 13: Risicoanalyse
*Technische, licentie en operationele risico's | Gegenereerd: 2026-05-29*

---

## Risico Classificatie

- **Ernst:** Laag / Midden / Hoog / Kritiek
- **Kans:** Onwaarschijnlijk / Mogelijk / Waarschijnlijk / Zeker
- **Status:** Onbevestigd / Aanname / Feit / Geverifieerd

---

## 13.1 Technische Risico's

### RT-001: Browser WebSocket audio latency (Kritiek / Zeker)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Kritiek** |
| Kans | **Zeker** — inherent aan browser audio model |
| Status | Feit |

**Beschrijving:**  
Zeus's browser-based audio streaming via WebSocket + Web Audio API introduceert 50-150ms end-to-end latency. Voor SSB/CW operatie is dit onaanvaardbaar (< 30ms gewenst).

**Impact:**  
Operator hoort zichzelf met > 100ms vertraging in de monitor speaker. CW niet bruikbaar via browser audio.

**Mitigatie:**  
✅ Primaire audio altijd via **miniaudio native output** op de host machine (Photino.NET desktop wrapper)  
✅ Browser audio = remote monitoring modus only (expliciet gedocumenteerd in UI)  
✅ Mobile (Capacitor) = monitoring only, geen TX audio via mobile  
✅ Zeus's `DspPipelineService` draineert al naar miniaudio native; dit is het correcte pad

**Residueel risico:** Laag — als gebruiker Photino.NET desktop gebruikt

---

### RT-002: .NET GC pauses in DSP hot path (Hoog / Mogelijk)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Hoog** |
| Kans | **Mogelijk** |
| Status | Aanname (niet gemeten) |

**Beschrijving:**  
De .NET Garbage Collector kan DSP-threads pauzeren voor 1-50ms bij high-allocation scenarios. WDSP verwacht continue IQ-feed zonder onderbrekingen.

**Impact:**  
Audio artefacten (pops, klikken), waterfall "gaps", S-meter fluctuaties.

**Mitigatie:**  
✅ `GC.TryStartNoGCRegion()` al geïmplementeerd in Zeus DSP loop (bevestigd in code)  
✅ `ServerGarbageCollection=false` in OpenhpsdrZeus.csproj (workstation GC = lagere pauses)  
✅ `Span<T>` en `ArrayPool<T>` gebruiken in hot path (zero-alloc patterns)  
✅ `Thread.Priority = ThreadPriority.Highest` voor DSP worker thread  

**Aanbeveling:**  
Meten met BenchmarkDotNet + EventSource GC probe tijdens 30-minuten operatiesessie.

---

### RT-003: WDSP channel ID conflict bij multi-device (Hoog / Mogelijk)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Hoog** |
| Kans | **Mogelijk** |
| Status | Aanname (WDSP broncode niet volledig geanalyseerd) |

**Beschrijving:**  
WDSP gebruikt globale arrays gedimensioneerd op MAX_CHANNELS. Als twee `WdspDspEngine` instances in één process dezelfde channel IDs gebruiken, kan data corruptie of crashes optreden.

**Impact:**  
Audio/DSP corruptie, applicatiecrash, data race conditions.

**Mitigatie:**  
✅ `WdspChannelAllocator` implementeren: primary range 0-13, aux range 16-29  
✅ Unit tests: channel allocatie conflict detectie  

**Te verifiëren:**  
Check in WDSP broncode (`RXA.c`) of channels array thread-safe is bij simultaan gebruik van verschillende channel IDs.

---

### RT-004: SoapySDR driver threading instabiliteit (Midden / Mogelijk)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Mogelijk** |
| Status | Aanname |

**Beschrijving:**  
SoapySDR's async streaming API is per driver geïmplementeerd en niet altijd thread-safe. Sommige drivers (bijv. SoapyRTLSDR) crashen bij aanroepen vanuit meerdere threads.

**Mitigatie:**  
✅ `SoapySdrSource` wraps alle native aanroepen in één dedicated native thread  
✅ Communiceer via `Channel<IqBlock>` (thread-safe producer/consumer)  
✅ Test alle drivers op alle target platforms in CI

---

### RT-005: RTL-SDR USB bandwidth bij meerdere dongels (Laag / Mogelijk)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Laag** |
| Kans | **Mogelijk** |
| Status | Aanname |

**Beschrijving:**  
Twee RTL-SDR dongels op dezelfde USB controller delen de 480 Mbps USB 2.0 bandbreedte. Bij 3.2 MHz × 2 bytes × 2 channels = 12.8 MB/s per dongel kan dit bottleneck zijn.

**Mitigatie:**  
Gebruik USB 3.0 hub; verspreid over meerdere USB controllers. Documenteer als hardware aanbeveling.

---

### RT-006: librtlsdr buffer latency (Midden / Zeker)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Zeker** |
| Status | Feit |

**Beschrijving:**  
librtlsdr standaard buffer = 8 × 16384 bytes ≈ 64ms latency @ 2.048 MHz. Dit is significant hoger dan Brick2's 21ms.

**Impact:**  
RTL-SDR audio loopt 40-60ms achter op primary Brick2 audio. Geen synchrone audio mogelijk.

**Mitigatie:**  
✅ RTL-SDR als "spectrum monitor" positioneren, niet als gesynchroniseerde ontvanger  
✅ Documenteer als bekende beperking in UI ("RX2 is niet gesynchroniseerd met primary")  
✅ Optioneel: buffer verklein naar 2 × 16384 (verhoogt USB overhead)

---

### RT-007: PlutoSDR libiio netwerk latency (Midden / Onbevestigd)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Onwaarschijnlijk** |
| Status | **Onbevestigd** (aanname ~25-30ms) |

**Te verifiëren:**  
Meten van libiio `iio_buffer_refill()` RTT op LAN bij 2.5 MHz sample rate.

---

## 13.2 Licentierisico's

### LR-001: SDRplay API vs GPL (Kritiek / Zeker)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Kritiek** |
| Kans | **Zeker** |
| Status | **Feit** |

**Beschrijving:**  
De SDRplay API 3.x is proprietaire software met een eigen licentieovereenkomst. Linken van NovaSdr-kernel (GPL v2+) met SDRplay's proprietary library zou een GPL-schending kunnen zijn.

**Impact:**  
Onmogelijk om SDRplay support in de GPL-kern te distribueren.

**Mitigatie:**  
✅ SDRplay adapter implementeren als **optionele binary-only plugin** buiten GPL-kern  
✅ Gebruiker installeert SDRplay API 3.x zelfstandig  
✅ NovaSdr laadt via `IZeusPlugin` dynamisch — runtime linking (LGPL-model)  
✅ Documenteer expliciet: "SDRplay support vereist apart geïnstalleerde SDRplay API"  

**Verificatie nodig:**  
SDRplay neem contact op voor duidelijkheid over distributie in GPL-context.

---

### LR-002: Thetis dual-license scope (Midden / Onbevestigd)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Mogelijk** |
| Status | **Onbevestigd** |

**Beschrijving:**  
Richard Samphire (MW0LGE) heeft een dual-license statement voor zijn eigen code in Thetis. Maar Thetis bevat ook code van FlexRadio Systems (GPL v2 only) en andere bijdragers. Het is onduidelijk of MW0LGE's code volledig te scheiden is van de GPL v2-only code.

**Impact:**  
Als we Thetis-code letterlijk overnemen, riskeren we GPL v2 conflicten.

**Mitigatie:**  
✅ **Geen Thetis code letterlijk overnemen** in NovaSdr  
✅ Thetis enkel als **referentie** gebruiken (lees de logica, implementeer zelf)  
✅ Documenteer dit besluit als architectuurregel

---

### LR-003: WDSP licentie (Laag / Geverifieerd)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Laag** |
| Kans | **Onwaarschijnlijk** |
| Status | **Geverifieerd** |

**Beschrijving:**  
WDSP (Warren Pratt NR0V) is GPL v2 of later. NovaSdr is ook GPL v2+. Compatibel.

**Mitigatie:** N.v.t. — geen actie vereist.

---

### LR-004: libiio LGPL dynamisch linken (Laag / Geverifieerd)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Laag** |
| Kans | **Onwaarschijnlijk** |
| Status | **Geverifieerd** |

**Beschrijving:**  
libiio is LGPL v2.1. LGPL staat dynamisch linken toe vanuit GPL-software zonder conflict.

**Mitigatie:**  
✅ Altijd dynamisch linken met libiio (geen static linking)

---

## 13.3 Protocolrisico's

### PR-001: Brick2-specifieke P2 registers onbekend (Midden / Onbevestigd)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Mogelijk** |
| Status | **Onbevestigd** |

**Beschrijving:**  
De drie primaire codebases behandelen Brick2 als generieke OpenHPSDR P2 radio. Het is onbekend of Brick2 specifieke registers, commands of gedrag heeft dat afwijkt van ANAN G2.

**Impact:**  
Verkeerde register-instellingen kunnen TX power, filtering of PA beschadigen.

**Mitigatie:**  
✅ Start met Brick2 in RX-only modus  
✅ Vergelijk met werkende deskHPSDR implementatie  
✅ Documenteer Brick2-specifieke bevindingen in `RadioCalibrations.cs`  
✅ Hardware beschikbaar (bevestigd door gebruiker) — test systematisch

---

### PR-002: Protocol 1 timing bij hoge CPU load (Midden / Mogelijk)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Mogelijk** |
| Status | **Aanname** |

**Beschrijving:**  
P1 TX pacing is semaphore-driven op RX packet arrival. Bij hoge CPU load (multi-device) kan RX packet processing vertragen, wat TX pacing verstoort en HL2 TX FIFO uitput.

**Impact:**  
TX audio artefacten, relay chatter, TX FIFO underruns.

**Mitigatie:**  
✅ DSP thread op `ThreadPriority.Highest` (al aanwezig in Zeus)  
✅ Windows timer resolution `timeBeginPeriod(1)` (al aanwezig in Zeus)  
✅ CPU profiling bij multi-device operatie (fase MVP validatie)

---

## 13.4 Realtime/Audio Risico's

### RA-001: ASIO support ontbreekt in NovaSdr MVP (Laag / Zeker)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Laag** |
| Kans | **Zeker** |
| Status | **Feit** |

**Beschrijving:**  
NovaSdr MVP heeft geen ASIO support. Thetis biedt ASIO voor < 5ms latency op Windows.

**Impact:**  
Windows power users met ASIO audio interfaces (Focusrite, RME) krijgen hogere latency.

**Mitigatie:**  
✅ miniaudio WASAPI exclusive mode als alternatief (10-20ms, acceptabel)  
✅ ASIO plugin in fase 2/3 roadmap  
✅ Documenteer als bekende beperking

---

### RA-002: Audio echo bij TX monitoring (Midden / Mogelijk)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Mogelijk** |
| Status | **Aanname** |

**Beschrijving:**  
Wanneer TX monitor audio (TX IQ loopback of mic monitor) via dezelfde audio device wordt afgespeeld als de mic, kan echo ontstaan.

**Mitigatie:**  
✅ TX monitor apart routeerbaar maken van RX audio (andere audio device of mute-optie)  
✅ Headphones strongly recommended (documenteer)

---

## 13.5 Crossplatform Risico's

### CP-001: Photino.NET WebView2 op Windows vereist Edge runtime (Laag / Zeker)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Laag** |
| Kans | **Zeker** |
| Status | **Feit** |

**Beschrijving:**  
Photino.NET op Windows gebruikt WebView2 (Microsoft Edge Chromium-based). Windows 10 21H2+ heeft WebView2 standaard geïnstalleerd. Oudere Windows 10 builds hebben het niet.

**Mitigatie:**  
✅ WebView2 Evergreen installer meenemen in setup  
✅ Documenteer minimale versievereiste: Windows 10 21H2+

---

### CP-002: Linux audio configuratie variabiliteit (Midden / Mogelijk)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Mogelijk** |
| Status | **Aanname** |

**Beschrijving:**  
Linux heeft meerdere audio subsystems (ALSA, PulseAudio, PipeWire). miniaudio gebruikt het beschikbare backend, maar PipeWire compatibiliteit kan variëren.

**Mitigatie:**  
✅ Test op Ubuntu 24.04 (PipeWire default), Fedora (PipeWire), Debian (PulseAudio)  
✅ miniaudio ondersteunt PipeWire via PulseAudio compatibility layer  
✅ Documenteer audio backend selectie opties

---

### CP-003: macOS code signing voor distributie (Midden / Zeker)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Zeker** |
| Status | **Feit** |

**Beschrijving:**  
macOS Gatekeeper vereist code signing voor distributie buiten App Store. deskHPSDR documenteert `xattr -cr` workaround — niet geschikt voor productie.

**Mitigatie:**  
✅ Apple Developer Program account vereist (€99/jaar)  
✅ Notarisatie via Xcode Notarytool  
✅ Ad-hoc signing voor development builds (geen kosten)

---

## 13.6 UI Complexiteitsrisico's

### UI-001: Multi-device layout overweldigend voor nieuwe gebruikers (Midden / Mogelijk)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Mogelijk** |
| Status | **Aanname** |

**Beschrijving:**  
Twee spectra, twee VFO-panels, TX-controls, meters, band awareness — dit kan overweldigend zijn voor beginners.

**Mitigatie:**  
✅ "Beginner mode" in UI: verberg RX2 panel als geen aux device verbonden  
✅ Progressieve onthulling: geavanceerde features in collapsable secties  
✅ Default layout: single-device view, RX2 panel optioneel toevoegen

---

### UI-002: WebGL panadapter op embedded/ARM GPU (Laag / Mogelijk)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Laag** |
| Kans | **Mogelijk** |
| Status | **Aanname** |

**Beschrijving:**  
Raspberry Pi en ARM-based devices hebben beperkte GPU capaciteit. WebGL waterfall bij 262144-point FFT kan frame drops veroorzaken.

**Mitigatie:**  
✅ FFT size configureerbaar (bestaande Zeus feature)  
✅ Configureerbare waterfall FPS (bestaande Zeus feature)  
✅ Canvas 2D fallback voor low-end GPU's

---

## 13.7 Mobile Haalbaarheidsrisico's

### MB-001: Capacitor audio latency voor monitoring (Laag / Zeker)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Laag** (bewust geaccepteerd) |
| Kans | **Zeker** |
| Status | **Feit** |

**Beschrijving:**  
Web Audio API op iOS heeft ~150-300ms latency. Android is beter maar nog steeds > 100ms.

**Beslissing:**  
✅ Mobile = spectrum viewing + frequency control only  
✅ Geen TX via mobile (architectureel uitgesloten)  
✅ Audio op mobile = monitoring only met expliciete latency disclaimer

---

## 13.8 Integratierisico's

### IR-001: WSJT-X integratie via VAC/IPC (Midden / Mogelijk)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Midden** |
| Kans | **Mogelijk** |
| Status | **Aanname** |

**Beschrijving:**  
WSJT-X verwacht een directe audio-feed via Virtual Audio Cable (Windows) of audio loopback (Linux/macOS). NovaSdr's miniaudio-gebaseerde audio routing moet compatibel zijn.

**Mitigatie:**  
✅ Documenteer audio routing setup voor WSJT-X  
✅ VAC bridge plugin in fase 2 roadmap  
✅ Alternatief: TCI audio streaming (WSJT-X kan via TCI audio ontvangen in nieuwere versies)

---

### IR-002: N1MM logging UDP packet format compatibiliteit (Laag / Onbevestigd)

| Eigenschap | Waarde |
|---|---|
| Ernst | **Laag** |
| Kans | **Onwaarschijnlijk** |
| Status | **Onbevestigd** |

**Beschrijving:**  
N1MM Logger+ verwacht specifieke UDP packet formats voor spectrum overlay. Thetis `N1MM.cs` is de referentie. Verificatie nodig dat het protocol niet gewijzigd is.

**Mitigatie:**  
✅ Verifieer N1MM UDP protocol documentatie  
✅ Test met N1MM Logger+ v1.0.11.x

---

## 13.9 Risicosamenvatting Matrix

| ID | Risico | Ernst | Kans | Prioriteit |
|---|---|---|---|---|
| RT-001 | Browser audio latency | Kritiek | Zeker | **1 — Architectuur** |
| LR-001 | SDRplay GPL conflict | Kritiek | Zeker | **2 — Distributie** |
| RT-003 | WDSP channel ID conflict | Hoog | Mogelijk | **3 — MVP** |
| PR-001 | Brick2 P2 registers | Midden | Mogelijk | **4 — Hardware test** |
| RT-002 | GC pauses in DSP | Hoog | Mogelijk | **5 — Performance** |
| LR-002 | Thetis code overname | Midden | Mogelijk | **6 — Governance** |
| CP-003 | macOS code signing | Midden | Zeker | **7 — Distributie** |
| MB-001 | Capacitor audio latency | Laag | Zeker | **8 — Scope beslissing** |
| RT-004 | SoapySDR threading | Midden | Mogelijk | **9 — Fase 1** |
| RT-006 | RTL-SDR buffer latency | Midden | Zeker | **10 — Scope** |

---

## 13.10 Risk Owner Toewijzingen (Aanbeveling)

| Risico | Owner | Deadline |
|---|---|---|
| RT-001 Browser audio | Architect | Voor MVP release |
| LR-001 SDRplay GPL | Jurist/Lead | Voor Fase 2 release |
| RT-003 WDSP channels | DSP Engineer | Sprint 2 |
| PR-001 Brick2 registers | Hardware Engineer | Sprint 6 |
| RT-002 GC pauses | DSP Engineer | Sprint 6 (meten) |
