namespace Faultline.Domain;

public class IssueComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IssueId { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string Body { get; set; } = default!;
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Issue Issue { get; set; } = default!;
    public User? AuthorUser { get; set; }
}
