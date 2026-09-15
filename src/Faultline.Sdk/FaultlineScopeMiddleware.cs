using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Faultline.Sdk;

/// <summary>Pushes a per-request scope so errors captured during the request carry route/method/correlation ID.</summary>
public class FaultlineScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using var _ = FaultlineScope.Push();

        FaultlineScope.SetTag("http.method", context.Request.Method);
        FaultlineScope.SetTag("http.route", context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "unknown");
        FaultlineScope.SetTag("correlation.id", context.TraceIdentifier);

        await next(context);
    }
}

public static class FaultlineApplicationBuilderExtensions
{
    /// <summary>Register early in the pipeline so the scope covers the whole request, including later middleware.</summary>
    public static IApplicationBuilder UseFaultlineScope(this IApplicationBuilder app) =>
        app.UseMiddleware<FaultlineScopeMiddleware>();
}
