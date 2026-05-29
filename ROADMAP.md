# NovaSdr Roadmap

Zie [docs/analysis/12_migratieplan.md](docs/analysis/12_migratieplan.md) voor het volledige migratieplan.

## MVP (0-3 maanden)
- [ ] Fork OpenHPSDR-Zeus als startpunt
- [ ] `IDeviceSource` / `ITransceiver` hardware abstraction layer
- [ ] `RtlSdrSource` — RTL-SDR als eerste RX2 device
- [ ] `DeviceCoordinatorService` — PTT lockout
- [ ] `Rx2PipelineService` — tweede DSP pipeline
- [ ] React multi-device panels (primary + RX2 side-by-side)
- [ ] End-to-end test: Brick2 P2 + RTL-SDR RX2

## Fase 2 (3-6 maanden)
- [ ] `SdrplaySource` — SDRplay API 3.x (user-installed)
- [ ] `PlutoSdrSource` / `PlutoSdrTransceiver` — libiio
- [ ] CAT plugin — Kenwood TS-2000 compatibel (TCP 4532)
- [ ] N1MM plugin — UDP spectrum streaming
- [ ] Station profiles — multi-device sessies opslaan/laden
- [ ] Frequentiesync RX2 ↔ primary VFO

## Fase 3 (6-12 maanden)
- [ ] DX Cluster plugin
- [ ] Solar / greyline propagatie
- [ ] FT8/WSPR monitor plugin
- [ ] Capacitor iOS/Android tablet UI
- [ ] ADIF QSO logging
- [ ] PureSignal via PlutoPlus feedback (experimenteel)
