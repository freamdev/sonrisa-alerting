using System.Net;
using Alerts.Api.Application;
using Alerts.Api.Domain;
using Alerts.Api.Infrastructure;
using Alerts.Api.Infrastructure.Identity;
using Alerts.Api.Infrastructure.News;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Alerts.UnitTests;

public sealed class NewsSourceTests
{
    private const string Valid = """{"id":"one","topic":"finance","title":"Update","body":"Content","timestamp":"2026-10-31T13:00:00+01:00"}""";

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("{}")]
    [InlineData("{\"id\":false}")]
    [InlineData("{\"id\":\"\"}")]
    public async Task MalformedItemsDoNotHideValidNeighbors(string invalid)
    {
        await using var db = await DatabaseAsync();
        var source = Source(db, new Handler((_, _) => Task.FromResult(Json($"[{invalid},{Valid}]"))));
        var alert = Assert.Single(await source.ReadAsync(default));
        Assert.Equal("one", alert.ExternalEventId);
        Assert.Equal(1, alert.TopicId);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero), alert.OccurredAt);
    }

    [Theory]
    [InlineData("\"one\"", "\"\"", "\"finance\"", "\"Content\"", "\"2026-10-31T12:00:00Z\"")]
    [InlineData("\"one\"", "\"Title\"", "\"unknown\"", "\"Content\"", "\"2026-10-31T12:00:00Z\"")]
    [InlineData("\"one\"", "\"Title\"", "\"Finance\"", "\"Content\"", "\"2026-10-31T12:00:00Z\"")]
    [InlineData("\"one\"", "\"Title\"", "\"finance\"", "null", "\"2026-10-31T12:00:00Z\"")]
    [InlineData("\"one\"", "\"Title\"", "\"finance\"", "\"Content\"", "\"2026-10-31T12:00:00\"")]
    [InlineData("\"one\"", "\"Title\"", "\"finance\"", "\"Content\"", "\"badZ\"")]
    public async Task InvalidFieldsAreRejected(string id, string title, string topic, string body, string timestamp)
    {
        await using var db = await DatabaseAsync();
        var json = $"[{{\"id\":{id},\"title\":{title},\"topic\":{topic},\"body\":{body},\"timestamp\":{timestamp}}}]";
        Assert.Empty(await Source(db, new Handler((_, _) => Task.FromResult(Json(json)))).ReadAsync(default));
    }

    [Fact]
    public async Task OversizedIdAndTitleAreRejected()
    {
        await using var db = await DatabaseAsync();
        foreach (var json in new[] { Valid.Replace("one", new string('a', 257)), Valid.Replace("Update", new string('a', 501)) })
            Assert.Empty(await Source(db, new Handler((_, _) => Task.FromResult(Json($"[{json}]")))).ReadAsync(default));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    public async Task InvalidEnvelopeFailsPoll(string json)
    {
        await using var db = await DatabaseAsync();
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() =>
            Source(db, new Handler((_, _) => Task.FromResult(Json(json)))).ReadAsync(default));
    }

    [Fact]
    public async Task HttpFailureAndTimeoutRemainSourceFailures()
    {
        await using var db = await DatabaseAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => Source(db, new Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))).ReadAsync(default));
        await Assert.ThrowsAsync<TaskCanceledException>(() => Source(db, new Handler((_, _) =>
            throw new TaskCanceledException("synthetic timeout"))).ReadAsync(default));
        Assert.Empty(db.Deliveries);
    }

    [Fact]
    public async Task HttpRequestReceivesCancellation()
    {
        await using var db = await DatabaseAsync();
        using var cts = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = Source(db, new Handler(async (_, ct) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return Json("[]");
        }));
        var read = source.ReadAsync(cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
    }

    [Fact]
    public async Task RepeatedRestEventsProduceOnlyOneDelivery()
    {
        await using var db = await DatabaseAsync();
        db.Users.Add(new() { Id = "alice" });
        db.UserTopicSelections.Add(new() { UserId = "alice", TopicId = 1 });
        db.UserChannelSelections.Add(new() { UserId = "alice", ChannelId = 1 });
        await db.SaveChangesAsync();
        using var gate = new DispatchGate();
        var sender = new FakeChannelSender("email");
        var dispatcher = new AlertDispatcher(db, [sender], gate, TimeProvider.System, NullLogger<AlertDispatcher>.Instance);
        var source = Source(db, new Handler((_, _) => Task.FromResult(Json($"[{Valid},{Valid}]"))));
        for (var poll = 0; poll < 2; poll++)
            foreach (var alert in await source.ReadAsync(default)) await dispatcher.ProcessAsync(alert);
        Assert.Single(db.AlertEvents);
        var delivery = Assert.Single(db.Deliveries);
        Assert.Equal(DeliveryStatus.Succeeded, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Single(sender.Calls);
    }

    [Fact]
    public async Task WorkerContinuesAfterSourceFailureAndCancelsActivePollWithoutOverlap()
    {
        var calls = 0;
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new StubSource(async ct =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new HttpRequestException("synthetic");
            blocked.SetResult();
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { cancelled.SetResult(); }
            return [];
        });
        using var services = new ServiceCollection().AddSingleton<INewsSource>(source)
            .AddSingleton<IAlertDispatcher>(new StubDispatcher()).BuildServiceProvider();
        using var worker = Worker(services);
        await worker.StartAsync(default);
        await blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, calls);
        await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, calls);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ShutdownCancelsIntervalAndDisabledWorkerDoesNotPoll()
    {
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var services = new ServiceCollection().AddSingleton<INewsSource>(new StubSource(_ =>
        {
            read.TrySetResult();
            return Task.FromResult<IReadOnlyList<AlertEvent>>([]);
        })).AddSingleton<IAlertDispatcher>(new StubDispatcher()).BuildServiceProvider();
        using (var disabled = Worker(services, enabled: false))
        {
            await disabled.StartAsync(default);
            await disabled.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(read.Task.IsCompleted);
        }
        using var worker = Worker(services, interval: 86400);
        await worker.StartAsync(default);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    private static NewsPollingWorker Worker(ServiceProvider services, bool enabled = true, int interval = 1) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(new NewsSourceOptions
        { Enabled = enabled, IntervalSeconds = interval }), TimeProvider.System, NullLogger<NewsPollingWorker>.Instance);

    private sealed class StubSource(Func<CancellationToken, Task<IReadOnlyList<AlertEvent>>> action) : INewsSource
    {
        public Task<IReadOnlyList<AlertEvent>> ReadAsync(CancellationToken cancellationToken) => action(cancellationToken);
    }
    private sealed class StubDispatcher : IAlertDispatcher
    {
        public Task<bool> ProcessAsync(AlertEvent alert, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RetryPendingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private static HttpNewsSource Source(AlertsIdentityDbContext db, HttpMessageHandler handler) =>
        new(new HttpClient(handler), db, Options.Create(new NewsSourceOptions()), NullLogger<HttpNewsSource>.Instance);
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request, cancellationToken);
    }
    private static async Task<AlertsIdentityDbContext> DatabaseAsync()
    {
        var db = new AlertsIdentityDbContext(new DbContextOptionsBuilder<AlertsIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
