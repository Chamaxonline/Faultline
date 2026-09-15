using System.Collections.Concurrent;

namespace Faultline.Sdk.Reliability;

/// <summary>
/// Tracks counts of events the SDK failed to deliver, by reason, so silent data
/// loss doesn't stay silent. Drained periodically by <see cref="FaultlineClientReportService"/>.
/// </summary>
internal static class FaultlineClientReport
{
    private static readonly ConcurrentDictionary<string, int> Counts = new();

    public static void RecordDrop(string reason) => Counts.AddOrUpdate(reason, 1, (_, count) => count + 1);

    public static Dictionary<string, int> DrainCounts()
    {
        var snapshot = new Dictionary<string, int>(Counts);
        foreach (var key in snapshot.Keys)
            Counts.TryRemove(key, out _);
        return snapshot;
    }
}
