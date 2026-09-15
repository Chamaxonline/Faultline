using System.Net;
using System.Net.Http.Json;
using Faultline.Domain.Contracts;
using Faultline.Sdk;
using Faultline.Sdk.Reliability;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faultline.Tests;

public class FaultlineReliabilityTests
{
    public FaultlineReliabilityTests()
    {
        FaultlineScope.Clear();
        FaultlineClientReport.DrainCounts(); // reset any leftover counts from other tests
    }

    private class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    private class SucceedingHandler : HttpMessageHandler
    {
        public List<ErrorEvent> Received = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Received.Add((await request.Content!.ReadFromJsonAsync<ErrorEvent>(cancellationToken: ct))!);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }

    [Fact]
    public async Task FailedSend_WithoutOfflineQueue_RecordsDrop()
    {
        var httpClient = new HttpClient(new FailingHandler()) { BaseAddress = new Uri("http://x") };
        var client = new FaultlineClient(httpClient, Options.Create(new FaultlineOptions { ServerUrl = "http://x", ProjectKey = "k" }), NullLogger<FaultlineClient>.Instance);

        await client.CaptureExceptionAsync(new Exception("boom"));

        var counts = FaultlineClientReport.DrainCounts();
        Assert.Equal(1, counts["send_failed"]);
    }

    [Fact]
    public async Task FailedSend_WithOfflineQueue_WritesToDiskAndCanBeResent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"faultline-test-{Guid.NewGuid():N}");
        try
        {
            var options = new FaultlineOptions { ServerUrl = "http://x", ProjectKey = "k", OfflineQueueDirectory = tempDir };
            var failingClient = new FaultlineClient(
                new HttpClient(new FailingHandler()) { BaseAddress = new Uri("http://x") },
                Options.Create(options), NullLogger<FaultlineClient>.Instance);

            await failingClient.CaptureExceptionAsync(new InvalidOperationException("queued boom"));

            var queue = new FaultlineOfflineQueue(tempDir, options.OfflineQueueMaxFiles, NullLogger.Instance);
            var queued = queue.ReadAll().ToList();
            Assert.Single(queued);
            Assert.Equal("InvalidOperationException", queued[0].Event.ExceptionType.Split('.').Last());

            // simulate the retry service successfully resending it
            var succeedingHandler = new SucceedingHandler();
            var recoveredClient = new FaultlineClient(
                new HttpClient(succeedingHandler) { BaseAddress = new Uri("http://x") },
                Options.Create(options), NullLogger<FaultlineClient>.Instance);

            var (path, evt) = queued[0];
            Assert.True(await recoveredClient.SendRawAsync(evt, CancellationToken.None));
            queue.Remove(path);

            Assert.Empty(queue.ReadAll());
            Assert.Single(succeedingHandler.Received);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void OfflineQueue_DropsOldestFile_WhenOverCapacity()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"faultline-test-{Guid.NewGuid():N}");
        try
        {
            var queue = new FaultlineOfflineQueue(tempDir, maxQueuedFiles: 2, NullLogger.Instance);

            queue.Enqueue(new ErrorEvent { ExceptionType = "A", Message = "first" });
            Thread.Sleep(10); // ensure distinct creation timestamps for oldest-first eviction
            queue.Enqueue(new ErrorEvent { ExceptionType = "B", Message = "second" });
            Thread.Sleep(10);
            queue.Enqueue(new ErrorEvent { ExceptionType = "C", Message = "third" }); // should evict "first"

            var remaining = queue.ReadAll().Select(x => x.Event.Message).ToList();
            Assert.Equal(2, remaining.Count);
            Assert.DoesNotContain("first", remaining);
            Assert.Contains("second", remaining);
            Assert.Contains("third", remaining);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
