using System.Net.Http.Json;
using Faultline.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faultline.Infrastructure.Alerts;

/// <summary>
/// Posts an Adaptive Card to a Teams webhook. Microsoft retired the legacy
/// "Incoming Webhook" connector card format in favor of Power Automate workflow
/// webhooks, which expect the message wrapped as an Adaptive Card attachment — set
/// one up via "When a Teams webhook request is received" and paste its URL into
/// AlertOptions:TeamsWebhookUrl.
/// </summary>
public class TeamsAlertNotifier(HttpClient httpClient, IOptions<AlertOptions> options, ILogger<TeamsAlertNotifier> logger)
    : IAlertNotifier
{
    private readonly AlertOptions _options = options.Value;

    public Task NotifyNewIssueAsync(Project project, Issue issue, CancellationToken ct = default) =>
        SendAsync($"🆕 New issue in {project.Name}", issue, ct);

    public Task NotifyRegressionAsync(Project project, Issue issue, CancellationToken ct = default) =>
        SendAsync($"⚠️ Issue reopened in {project.Name}", issue, ct);

    private async Task SendAsync(string heading, Issue issue, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.TeamsWebhookUrl))
        {
            logger.LogDebug("Teams webhook not configured, skipping alert for issue {IssueId}", issue.Id);
            return;
        }

        var issueUrl = $"{_options.DashboardBaseUrl.TrimEnd('/')}/issues/{issue.Id}";

        var card = new
        {
            type = "message",
            attachments = new object[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new { type = "TextBlock", text = heading, weight = "Bolder", size = "Medium" },
                            new { type = "TextBlock", text = issue.Title, wrap = true },
                            new
                            {
                                type = "FactSet",
                                facts = new object[]
                                {
                                    new { title = "Level", value = issue.Level },
                                    new { title = "Seen", value = $"{issue.Count}x" },
                                    new { title = "Last seen", value = issue.LastSeen.ToString("u") }
                                }
                            }
                        },
                        actions = new object[]
                        {
                            new { type = "Action.OpenUrl", title = "View issue", url = issueUrl }
                        }
                    }
                }
            }
        };

        try
        {
            var response = await httpClient.PostAsJsonAsync(_options.TeamsWebhookUrl, card, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Teams alert failed with {StatusCode} for issue {IssueId}", response.StatusCode, issue.Id);
        }
        catch (Exception ex)
        {
            // alerting must never take the worker down
            logger.LogWarning(ex, "Failed to send Teams alert for issue {IssueId}", issue.Id);
        }
    }
}
