using System.Net;
using System.Net.Http.Json;
using Faultline.Domain.Contracts;
using Faultline.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faultline.Tests;

public class StackTraceQualityTests
{
    public StackTraceQualityTests() => FaultlineScope.Clear();

    private class CapturingHandler : HttpMessageHandler
    {
        public ErrorEvent? SentEvent;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            SentEvent = await request.Content!.ReadFromJsonAsync<ErrorEvent>(cancellationToken: ct);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }

    [Fact]
    public async Task CapturedFrame_FromThisTestAssembly_IsMarkedInApp()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5065") };
        var options = new FaultlineOptions
        {
            ServerUrl = "http://x",
            ProjectKey = "k",
            InAppAssemblyPrefixes = ["Faultline.Tests"]
        };
        var client = new FaultlineClient(httpClient, Options.Create(options), NullLogger<FaultlineClient>.Instance);

        Exception thrown;
        try
        {
            ThrowSomething();
            throw new InvalidOperationException("unreachable");
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        await client.CaptureExceptionAsync(thrown);

        Assert.NotEmpty(handler.SentEvent!.Frames);
        var frame = Assert.Single(handler.SentEvent.Frames, f => f.InApp && f.Function == nameof(ThrowSomething));
        Assert.NotNull(frame.ContextLines);
        Assert.Contains(frame.ContextLines!, line => line.Contains("throw new InvalidOperationException"));
    }

    [Fact]
    public async Task ContextLineCountZero_SkipsSourceContext()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5065") };
        var options = new FaultlineOptions { ServerUrl = "http://x", ProjectKey = "k", ContextLineCount = 0 };
        var client = new FaultlineClient(httpClient, Options.Create(options), NullLogger<FaultlineClient>.Instance);

        try
        {
            ThrowSomething();
        }
        catch (Exception ex)
        {
            await client.CaptureExceptionAsync(ex);
        }

        Assert.All(handler.SentEvent!.Frames, f => Assert.Null(f.ContextLines));
    }

    private static void ThrowSomething() => throw new InvalidOperationException("boom");
}
