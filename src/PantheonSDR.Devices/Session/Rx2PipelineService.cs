using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PantheonSDR.Devices.Session;

/// <summary>
/// Achtergrondservice die IQ-data van alle auxiliary devices verwerkt.
/// Per auxiliary device wordt een eigen verwerkingslus gestart die:
///   1. IqBlokken leest van het device (via StreamAsync)
///   2. Decimeert naar 48 kHz (via SampleRateBridge in AttachedDevice)
///   3. Doorgeeft aan de DSP-engine (via IDspFeedCallback)
///   4. Display-pixels en audio publiceert via events
///
/// Symmetrisch: primary en aux devices gebruiken hetzelfde pipeline-patroon.
/// Het onderscheid zit alleen in de WDSP-kanaalindeling.
/// </summary>
public sealed class Rx2PipelineService : BackgroundService
{
    private readonly RadioSession _session;
    private readonly IDspFeedCallback _dspCallback;
    private readonly ILogger<Rx2PipelineService> _logger;

    public Rx2PipelineService(
        RadioSession session,
        IDspFeedCallback dspCallback,
        ILogger<Rx2PipelineService> logger)
    {
        _session     = session;
        _dspCallback = dspCallback;
        _logger      = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rx2PipelineService gestart");

        // Start een pipeline-taak per auxiliary device
        // Bij attach/detach van devices worden taken dynamisch gestart/gestopt
        var pipelines = new Dictionary<string, CancellationTokenSource>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var currentAux = _session.Auxiliaries;

            // Start pipelines voor nieuw gekoppelde devices
            foreach (var aux in currentAux)
            {
                if (!pipelines.ContainsKey(aux.Device.DeviceId))
                {
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    pipelines[aux.Device.DeviceId] = cts;

                    // Start pipeline als fire-and-forget taak
                    _ = RunAuxPipelineAsync(aux, cts.Token);
                    _logger.LogInformation(
                        "RX2 pipeline gestart voor '{Name}' (WDSP ch {Ch})",
                        aux.Device.FriendlyName, aux.WdspChannelId);
                }
            }

            // Stop pipelines voor losgekoppelde devices
            var activeIds = currentAux.Select(a => a.Device.DeviceId).ToHashSet();
            foreach (var id in pipelines.Keys.Where(k => !activeIds.Contains(k)).ToList())
            {
                pipelines[id].Cancel();
                pipelines.Remove(id);
                _logger.LogInformation("RX2 pipeline gestopt voor device {Id}", id);
            }

            await Task.Delay(500, stoppingToken); // Poll elke 500 ms op nieuwe devices
        }

        // Opruimen
        foreach (var cts in pipelines.Values) cts.Cancel();
    }

    private async Task RunAuxPipelineAsync(AttachedDevice aux, CancellationToken ct)
    {
        try
        {
            await foreach (var rawBlock in aux.Device.StreamAsync(ct))
            {
                if (!aux.IsEnabled) continue;

                // Decimeer native rate → 48 kHz
                var block = aux.Decimate(rawBlock);
                if (block is null) continue;

                // Voer WDSP in op het toegewezen kanaal
                _dspCallback.FeedIq(aux.WdspChannelId, block.Samples);

                // Notificeer luisteraars (display, audio, meters)
                IqBlockProcessed?.Invoke(this, new IqBlockProcessedEventArgs(aux, block));
            }
        }
        catch (OperationCanceledException) { /* normaal afsluiten */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fout in RX2 pipeline voor '{Name}'", aux.Device.FriendlyName);
        }
    }

    /// <summary>Vuurt nadat een IQ-blok door de pipeline is verwerkt.</summary>
    public event EventHandler<IqBlockProcessedEventArgs>? IqBlockProcessed;
}

/// <summary>
/// Abstractie voor het doorgeven van IQ-data aan de WDSP DSP-engine.
/// Wordt geïmplementeerd door de Zeus.Dsp WdspDspEngine-wrapper.
/// </summary>
public interface IDspFeedCallback
{
    void FeedIq(int channelId, double[] interleavedIq);
}

/// <summary>Event-data na IQ-verwerking.</summary>
public sealed class IqBlockProcessedEventArgs(AttachedDevice device, IqBlock block) : EventArgs
{
    public AttachedDevice Device { get; } = device;
    public IqBlock Block { get; } = block;
}
