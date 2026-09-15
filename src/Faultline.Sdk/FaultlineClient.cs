using System.Net.Http.Json;
using Faultline.Domain.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faultline.Sdk;

public class FaultlineClient(HttpClient httpClient, IOptions<FaultlineOptions> options, ILogger<FaultlineClient> logger)
{
    private readonly FaultlineOptions _options = options.Value;

    public Task CaptureExceptionAsync(Exception exception, string level = "error", CancellationToken ct = default) =>
        CaptureAsync(ToErrorEvent(exception, level), ct);

    public async Task CaptureAsync(ErrorEvent evt, CancellationToken ct = default)
    {
        evt.Environment ??= _options.Environment;
        evt.Release ??= _options.Release;
        MergeScope(evt);

        try
        {
            var response = await httpClient.PostAsJsonAsync($"/api/v1/{_options.ProjectKey}/store", evt, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Faultline ingest returned {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            // reporting failures must never take down the host app
            logger.LogWarning(ex, "Failed to report exception to Faultline");
        }
    }

    private static void MergeScope(ErrorEvent evt)
    {
        var scope = FaultlineScope.Current;

        foreach (var (key, value) in scope.Tags)
            evt.Tags.TryAdd(key, value);

        evt.UserContext ??= scope.UserContext;
        evt.Breadcrumbs = [.. scope.Breadcrumbs, .. evt.Breadcrumbs];
    }

    private static ErrorEvent ToErrorEvent(Exception exception, string level)
    {
        var trace = new System.Diagnostics.StackTrace(exception, fNeedFileInfo: true);
        var frames = trace.GetFrames()?.Select(f => new StackFrame
        {
            Function = f.GetMethod()?.Name,
            File = f.GetFileName(),
            Line = f.GetFileLineNumber() is var line && line > 0 ? line : null
        }).ToList() ?? [];

        return new ErrorEvent
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            Frames = frames,
            Level = level
        };
    }
}
