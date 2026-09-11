namespace Faultline.Domain;

public enum IssueStatus
{
    Unresolved,
    Resolved,
    Ignored
}

public class Issue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Fingerprint { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? ExceptionType { get; set; }
    public string Level { get; set; } = "error";
    public IssueStatus Status { get; set; } = IssueStatus.Unresolved;
    public int Count { get; set; } = 1;
    public DateTimeOffset FirstSeen { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;

    // denormalized from the most recent Event so issue search/filter doesn't need a join
    public string? LastEnvironment { get; set; }
    public string? LastRelease { get; set; }

    public Project Project { get; set; } = default!;
    public List<Event> Events { get; set; } = [];
}
