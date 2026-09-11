using StackExchange.Redis;

namespace Faultline.Infrastructure.Queue;

/// <summary>
/// Redis Streams-backed queue. Chosen over Azure Service Bus / RabbitMQ for this
/// workload: single consumer group, low throughput, already running Redis-class
/// infra is unnecessary here — a stream on the same Redis instance keeps ops simple.
/// </summary>
public class RedisEventQueue(IConnectionMultiplexer redis) : IEventQueue
{
    private const string StreamKey = "faultline:events";
    private const string GroupName = "faultline-workers";

    public async Task PublishAsync(Guid projectId, string rawEventJson, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.StreamAddAsync(StreamKey,
        [
            new NameValueEntry("projectId", projectId.ToString()),
            new NameValueEntry("payload", rawEventJson)
        ]);
    }

    public async Task ConsumeAsync(Func<Guid, string, CancellationToken, Task> handler, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await EnsureGroupExistsAsync(db);

        var consumerName = $"worker-{System.Environment.MachineName}-{System.Environment.ProcessId}";

        while (!ct.IsCancellationRequested)
        {
            var entries = await db.StreamReadGroupAsync(StreamKey, GroupName, consumerName, ">", count: 10);

            if (entries.Length == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                continue;
            }

            foreach (var entry in entries)
            {
                var projectId = Guid.Parse(entry["projectId"].ToString());
                var payload = entry["payload"].ToString();

                await handler(projectId, payload, ct);
                await db.StreamAcknowledgeAsync(StreamKey, GroupName, entry.Id);
            }
        }
    }

    private static async Task EnsureGroupExistsAsync(IDatabase db)
    {
        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, StreamPosition.NewMessages, createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // group already exists — fine
        }
    }
}
