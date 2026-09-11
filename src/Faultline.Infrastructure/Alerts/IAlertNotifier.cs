using Faultline.Domain;

namespace Faultline.Infrastructure.Alerts;

public interface IAlertNotifier
{
    Task NotifyNewIssueAsync(Project project, Issue issue, CancellationToken ct = default);
    Task NotifyRegressionAsync(Project project, Issue issue, CancellationToken ct = default);
}
