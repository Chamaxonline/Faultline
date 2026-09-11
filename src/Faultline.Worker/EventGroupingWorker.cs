using System.Text.Json;
using Faultline.Domain;
using Faultline.Domain.Contracts;
using Faultline.Infrastructure;
using Faultline.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;

namespace Faultline.Worker;

/// <summary>
/// Consumes raw events off the queue, groups them into Issues by fingerprint,
/// and persists both the aggregate (Issue) and the raw Event.
/// </summary>
public class EventGroupingWorker(
    IEventQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<EventGroupingWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        queue.ConsumeAsync(HandleEventAsync, stoppingToken);

    private async Task HandleEventAsync(Guid projectId, string rawPayload, CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<ErrorEvent>(rawPayload)
            ?? throw new InvalidOperationException("could not deserialize queued event");

        var fingerprint = Fingerprint.Compute(evt);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FaultlineDbContext>();

        var issue = await db.Issues
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.Fingerprint == fingerprint, ct);

        if (issue is null)
        {
            issue = new Issue
            {
                ProjectId = projectId,
                Fingerprint = fingerprint,
                Title = $"{evt.ExceptionType}: {evt.Message}",
                ExceptionType = evt.ExceptionType,
                Level = evt.Level,
                FirstSeen = evt.Timestamp,
                LastSeen = evt.Timestamp,
                Count = 1
            };
            db.Issues.Add(issue);
            logger.LogInformation("New issue {Fingerprint} for project {ProjectId}: {Title}", fingerprint, projectId, issue.Title);
        }
        else
        {
            issue.Count++;
            issue.LastSeen = evt.Timestamp;

            // an event on a resolved issue means the bug is back — reopen it
            if (issue.Status == IssueStatus.Resolved)
                issue.Status = IssueStatus.Unresolved;
        }

        db.Events.Add(new Event
        {
            IssueId = issue.Id,
            ProjectId = projectId,
            Release = evt.Release,
            Environment = evt.Environment,
            UserContext = evt.UserContext,
            RawPayload = rawPayload,
            Timestamp = evt.Timestamp
        });

        await db.SaveChangesAsync(ct);
    }
}
