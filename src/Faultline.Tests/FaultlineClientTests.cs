using System.Net;
using System.Net.Http.Json;
using Faultline.Domain.Contracts;
using Faultline.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faultline.Tests;

public class FaultlineClientTests
{
    public FaultlineClientTests() => FaultlineScope.Clear();

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
    public async Task CaptureAsync_MergesScopeTagsUserAndBreadcrumbs_IntoOutgoingEvent()
    {
        FaultlineScope.SetTag("service", "checkout-api");
        FaultlineScope.SetUser("user-42");
        FaultlineScope.AddBreadcrumb("started checkout", "flow");

        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5065") };
        var options = Options.Create(new FaultlineOptions { ServerUrl = "http://localhost:5065", ProjectKey = "test-key" });
        var client = new FaultlineClient(httpClient, options, NullLogger<FaultlineClient>.Instance);

        await client.CaptureExceptionAsync(new InvalidOperationException("boom"));

        Assert.NotNull(handler.SentEvent);
        Assert.Equal("checkout-api", handler.SentEvent!.Tags["service"]);
        Assert.Equal("user-42", handler.SentEvent.UserContext);
        Assert.Single(handler.SentEvent.Breadcrumbs);
        Assert.Equal("started checkout", handler.SentEvent.Breadcrumbs[0].Message);
    }
}
