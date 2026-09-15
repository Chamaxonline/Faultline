namespace Faultline.Domain.Contracts;

public class Breadcrumb
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Message { get; set; } = default!;
    public string Category { get; set; } = "manual";
    public string Level { get; set; } = "info";
}
