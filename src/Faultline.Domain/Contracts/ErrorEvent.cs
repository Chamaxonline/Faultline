namespace Faultline.Domain.Contracts;

/// <summary>Wire contract sent by SDKs to the ingestion API.</summary>
public class ErrorEvent
{
    public string ExceptionType { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? StackTrace { get; set; }
    public List<StackFrame> Frames { get; set; } = [];
    public string Level { get; set; } = "error";
    public string? Release { get; set; }
    public string? Environment { get; set; }
    public string? UserContext { get; set; }
    public Dictionary<string, string> Tags { get; set; } = [];
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public List<Breadcrumb> Breadcrumbs { get; set; } = [];
}

public class StackFrame
{
    public string? Function { get; set; }
    public string? File { get; set; }
    public int? Line { get; set; }
}
