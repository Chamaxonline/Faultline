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
    public Dictionary<string, string> Extra { get; set; } = [];
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public List<Breadcrumb> Breadcrumbs { get; set; } = [];
}

public class StackFrame
{
    public string? Function { get; set; }
    public string? File { get; set; }
    public int? Line { get; set; }

    /// <summary>True if this frame belongs to the app's own code rather than a framework/library.</summary>
    public bool InApp { get; set; }

    /// <summary>Source lines surrounding <see cref="Line"/> (best-effort — only available if the source file is on disk).</summary>
    public List<string>? ContextLines { get; set; }

    /// <summary>Line number of the first entry in <see cref="ContextLines"/>.</summary>
    public int? ContextStartLine { get; set; }
}
