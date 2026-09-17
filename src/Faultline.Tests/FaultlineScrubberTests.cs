using System.Net;
using System.Net.Http.Json;
using Faultline.Contracts;
using Faultline.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faultline.Tests;

public class FaultlineScrubberTests
{
    public FaultlineScrubberTests() => FaultlineScope.Clear();

    private class CapturingHandler : HttpMessageHandler
    {
        public ErrorEvent? SentEvent;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            SentEvent = await request.Content!.ReadFromJsonAsync<ErrorEvent>(cancellationToken: ct);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }

    private static (FaultlineClient client, CapturingHandler handler) MakeClient(FaultlineOptions options)
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5065") };
        var client = new FaultlineClient(httpClient, Options.Create(options), NullLogger<FaultlineClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task DefaultScrubFieldNames_FilterMatchingTagsAndExtra_ByDefault()
    {
        FaultlineScope.SetTag("authToken", "super-secret");
        FaultlineScope.SetTag("region", "eu-west");
        FaultlineScope.SetExtra("dbConnectionString", "Server=x;Password=y");

        var (client, handler) = MakeClient(new FaultlineOptions { ServerUrl = "http://x", ProjectKey = "k" });
        await client.CaptureExceptionAsync(new Exception("boom"));

        Assert.Equal("[Filtered]", handler.SentEvent!.Tags["authToken"]);
        Assert.Equal("eu-west", handler.SentEvent.Tags["region"]);
        Assert.Equal("[Filtered]", handler.SentEvent.Extra["dbConnectionString"]);
    }

    [Fact]
    public async Task ScrubPatterns_FilterMatchingText_InMessageAndBreadcrumbs()
    {
        FaultlineScope.AddBreadcrumb("card number 4111111111111111 declined");

        var options = new FaultlineOptions
        {
            ServerUrl = "http://x",
            ProjectKey = "k",
            ScrubPatterns = [@"\b\d{16}\b"]
        };
        var (client, handler) = MakeClient(options);

        await client.CaptureExceptionAsync(new Exception("card 4111111111111111 failed"));

        Assert.DoesNotContain("4111111111111111", handler.SentEvent!.Message);
        Assert.DoesNotContain("4111111111111111", handler.SentEvent.Breadcrumbs[0].Message);
    }

    [Fact]
    public async Task NoScrubPatternsConfigured_LeavesFreeTextUntouched()
    {
        var (client, handler) = MakeClient(new FaultlineOptions { ServerUrl = "http://x", ProjectKey = "k" });

        await client.CaptureExceptionAsync(new Exception("card 4111111111111111 failed"));

        Assert.Equal("card 4111111111111111 failed", handler.SentEvent!.Message);
    }
}
