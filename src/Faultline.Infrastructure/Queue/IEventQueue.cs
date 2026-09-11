namespace Faultline.Infrastructure.Queue;

public interface IEventQueue
{
    Task PublishAsync(Guid projectId, string rawEventJson, CancellationToken ct = default);

    /// <summary>Reads new entries as they arrive, acking each after the handler completes.</summary>
    Task ConsumeAsync(Func<Guid, string, CancellationToken, Task> handler, CancellationToken ct = default);
}
