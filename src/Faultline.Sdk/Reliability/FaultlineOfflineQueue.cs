using System.Text.Json;
using Faultline.Domain.Contracts;
using Microsoft.Extensions.Logging;

namespace Faultline.Sdk.Reliability;

/// <summary>
/// Disk-backed buffer for events that failed to send. Opt-in (set
/// <see cref="FaultlineOptions.OfflineQueueDirectory"/>) — without it, a failed
/// send is just dropped and counted, matching prior behavior.
/// </summary>
internal class FaultlineOfflineQueue(string directory, int maxQueuedFiles, ILogger logger)
{
    public void Enqueue(ErrorEvent evt)
    {
        try
        {
            Directory.CreateDirectory(directory);

            var files = Directory.GetFiles(directory, "*.json");
            if (files.Length >= maxQueuedFiles)
            {
                var oldest = files.OrderBy(File.GetCreationTimeUtc).First();
                File.Delete(oldest);
                FaultlineClientReport.RecordDrop("offline_queue_full");
            }

            var path = Path.Combine(directory, $"{Guid.NewGuid():N}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(evt));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write event to Faultline offline queue");
            FaultlineClientReport.RecordDrop("offline_queue_write_failed");
        }
    }

    public IEnumerable<(string Path, ErrorEvent Event)> ReadAll()
    {
        if (!Directory.Exists(directory)) yield break;

        foreach (var file in Directory.GetFiles(directory, "*.json").OrderBy(f => f))
        {
            ErrorEvent? evt = null;
            try
            {
                evt = JsonSerializer.Deserialize<ErrorEvent>(File.ReadAllText(file));
            }
            catch
            {
                // corrupt queue file — remove it below rather than retrying forever
            }

            if (evt is not null)
                yield return (file, evt);
            else
                TryDelete(file);
        }
    }

    public void Remove(string path) => TryDelete(path);

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best-effort cleanup */ }
    }
}
