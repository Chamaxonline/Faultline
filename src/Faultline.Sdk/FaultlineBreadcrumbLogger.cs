using Microsoft.Extensions.Logging;

namespace Faultline.Sdk;

/// <summary>Turns ordinary ILogger calls (Info and above) into breadcrumbs on the current scope.</summary>
internal class FaultlineBreadcrumbLogger(string category, LogLevel minLevel) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        FaultlineScope.AddBreadcrumb(message, category, LevelToBreadcrumbLevel(logLevel));
    }

    private static string LevelToBreadcrumbLevel(LogLevel level) => level switch
    {
        LogLevel.Critical => "fatal",
        LogLevel.Error => "error",
        LogLevel.Warning => "warning",
        _ => "info"
    };
}

public class FaultlineBreadcrumbLoggerProvider(LogLevel minLevel = LogLevel.Information) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FaultlineBreadcrumbLogger(categoryName, minLevel);

    public void Dispose() { }
}

public static class FaultlineLoggingBuilderExtensions
{
    /// <summary>Feeds Info+ log calls into the current FaultlineScope as breadcrumbs.</summary>
    public static ILoggingBuilder AddFaultlineBreadcrumbs(this ILoggingBuilder builder, LogLevel minLevel = LogLevel.Information)
    {
        builder.AddProvider(new FaultlineBreadcrumbLoggerProvider(minLevel));
        return builder;
    }
}
