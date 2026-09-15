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
        FaultlineScrubber.Scrub(evt, _options);

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

        foreach (var (key, value) in scope.Extra)
            evt.Extra.TryAdd(key, value);

        evt.UserContext ??= scope.UserContext;
        evt.Breadcrumbs = [.. scope.Breadcrumbs, .. evt.Breadcrumbs];
    }

    private ErrorEvent ToErrorEvent(Exception exception, string level)
    {
        var trace = new System.Diagnostics.StackTrace(exception, fNeedFileInfo: true);
        var frames = trace.GetFrames()?.Select(BuildFrame).ToList() ?? [];

        return new ErrorEvent
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            Frames = frames,
            Level = level
        };
    }

    private StackFrame BuildFrame(System.Diagnostics.StackFrame f)
    {
        var method = f.GetMethod();
        var assemblyName = method?.DeclaringType?.Assembly.GetName().Name;
        var file = f.GetFileName();
        var line = f.GetFileLineNumber() is var l && l > 0 ? l : (int?)null;

        var frame = new StackFrame
        {
            Function = method?.Name,
            File = file,
            Line = line,
            InApp = assemblyName is not null &&
                    _options.InAppAssemblyPrefixes.Any(prefix =>
                        prefix.Length > 0 && assemblyName.StartsWith(prefix, StringComparison.Ordinal))
        };

        if (line is not null && file is not null && _options.ContextLineCount > 0)
            AttachContextLines(frame, file, line.Value);

        return frame;
    }

    private void AttachContextLines(StackFrame frame, string file, int line)
    {
        try
        {
            if (!File.Exists(file)) return;

            var allLines = File.ReadAllLines(file);
            var start = Math.Max(0, line - 1 - _options.ContextLineCount);
            var end = Math.Min(allLines.Length - 1, line - 1 + _options.ContextLineCount);

            frame.ContextLines = allLines[start..(end + 1)].ToList();
            frame.ContextStartLine = start + 1;
        }
        catch
        {
            // source not available at runtime (typical for published binaries) — frame stays without context
        }
    }
}
