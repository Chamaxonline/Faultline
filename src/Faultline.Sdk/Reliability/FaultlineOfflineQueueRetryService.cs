using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faultline.Sdk.Reliability;

/// <summary>Retries events buffered by <see cref="FaultlineOfflineQueue"/> on a timer. No-ops if OfflineQueueDirectory isn't set.</summary>
internal class FaultlineOfflineQueueRetryService(
    FaultlineClient client,
    IOptions<FaultlineOptions> options,
    ILogger<FaultlineOfflineQueueRetryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (string.IsNullOrEmpty(opts.OfflineQueueDirectory)) return;

        var queue = new FaultlineOfflineQueue(opts.OfflineQueueDirectory, opts.OfflineQueueMaxFiles, logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var (path, evt) in queue.ReadAll().ToList())
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    if (await client.SendRawAsync(evt, stoppingToken))
                        queue.Remove(path);
                    else
                        break; // API still unreachable — stop this pass, try again next tick
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Faultline offline queue retry pass failed");
            }

            try
            {
                await Task.Delay(opts.OfflineQueueRetryInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
