namespace Faultline.Infrastructure.Alerts;

public class AlertOptions
{
    /// <summary>Incoming webhook URL from a Teams channel connector. Null/empty disables Teams alerts.</summary>
    public string? TeamsWebhookUrl { get; set; }

    /// <summary>Dashboard base URL, used to build "view issue" links in alerts.</summary>
    public string DashboardBaseUrl { get; set; } = "http://localhost:3000";
}
