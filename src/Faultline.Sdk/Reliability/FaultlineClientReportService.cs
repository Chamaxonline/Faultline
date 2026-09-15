using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faultline.Sdk.Reliability;

/// <summary>Periodically logs a summary of dropped/queued events so silent data loss shows up in the host app's own logs.</summary>
internal class FaultlineClientReportService(IOptions<FaultlineOptions> options, ILogger<FaultlineClientReportService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.ClientReportInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var counts = FaultlineClientReport.DrainCounts();
            if (counts.Count == 0) continue;

            var summary = string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}"));
            logger.LogWarning("Faultline SDK reliability report — dropped/queued events in the last {Interval}: {Summary}", interval, summary);
        }
    }
}
