# PantheonSDR Roadmap

See [docs/analysis/12_migratieplan.md](docs/analysis/12_migratieplan.md) for the full migration plan.

## MVP (months 0–3)
- [x] Architecture analysis & 18-document master report
- [x] `IDeviceSource` / `ITransceiver` hardware abstraction layer
- [x] `WdspChannelAllocator` — WDSP channel partitioning (primary 0–13, aux 16–29)
- [x] `DeviceRegistry` — device discovery registry with events
- [x] `SdrplaySource` — SDRplay RSP1A (native API 3.x)
- [x] `PlutoSdrTransceiver` — PlutoSDR Plus F5OEO (libiio, 70 MHz–6 GHz)
- [x] `SampleRateBridge` — polyphase FIR decimation to 48 kHz
- [x] `RadioSession` — symmetric N-device session model
- [x] `DeviceCoordinatorService` — PTT lockout + frequency sync
- [x] `Rx2PipelineService` — dynamic DSP pipelines per auxiliary device
- [ ] Wire `RadioSession` into `Zeus.Server.Hosting`
- [ ] Multi-device React panels
- [ ] End-to-end test: Brick2 P2 + SDRplay RSP1A

## Phase 2 (months 3–6)
- [ ] CAT plugin (Kenwood TS-2000 compatible)
- [ ] N1MM Logger+ UDP streaming plugin
- [ ] RTL-SDR adapter (librtlsdr)
- [ ] Station profiles — save/load multi-device sessions
- [ ] DX spot overlay on panadapter
- [ ] Saturn G2 P2 extensions

## Phase 3 (months 6–12)
- [ ] DX Cluster plugin (telnet)
- [ ] Solar / greyline propagation plugin
- [ ] FT8/WSPR monitor plugin
- [ ] Capacitor iOS/Android tablet optimisations
- [ ] ADIF QSO logging
- [ ] PureSignal via PlutoSDR feedback (experimental)
