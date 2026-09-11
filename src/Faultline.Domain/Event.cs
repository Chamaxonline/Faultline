namespace Faultline.Domain;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IssueId { get; set; }
    public Guid ProjectId { get; set; }
    public string? Release { get; set; }
    public string? Environment { get; set; }
    public string? UserContext { get; set; }
    public string RawPayload { get; set; } = default!;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public Issue Issue { get; set; } = default!;
}
