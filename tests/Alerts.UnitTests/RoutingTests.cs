using Alerts.Api.Application;
using Alerts.Api.Domain;
using Alerts.Api.Infrastructure;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit.Abstractions;

namespace Alerts.UnitTests;

public sealed class RoutingTests(ITestOutputHelper output)
{
    [Fact]
    public async Task StartupRecoveryResolvesDispatcherAndSendsPersistedPendingRows()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.AlertEvents.Add(Event("startup"));
        await f.Db.SaveChangesAsync();
        f.Db.Deliveries.Add(new() { EventId = (await f.Db.AlertEvents.SingleAsync()).Id, UserId = "alice", ChannelId = 1 });
        await f.Db.SaveChangesAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new Microsoft.Extensions.Hosting.Internal.HostingEnvironment());
        services.AddScoped(_ => new AlertsIdentityDbContext(f.Options));
        services.AddAlertRouting(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.RemoveAll<IChannelSender>();
        services.AddSingleton<IChannelSender>(new FakeChannelSender("email"));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var recovery = Assert.IsType<PendingDeliveryRecovery>(Assert.Single(provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()));
        await recovery.StartAsync(CancellationToken.None);
        await recovery.ExecuteTask!;
        await recovery.StopAsync(CancellationToken.None);
        await using var reopened = new AlertsIdentityDbContext(f.Options);
        Assert.Equal(DeliveryStatus.Succeeded, (await reopened.Deliveries.SingleAsync()).Status);
        var fake = Assert.IsType<FakeChannelSender>(provider.GetServices<IChannelSender>().Single(x => x.ChannelCode == "email"));
        Assert.Single(fake.Calls);
    }

    [Fact]
    public async Task MissingAdapterLeavesDeliveryPendingWithoutConsumingAttempts()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Dispatcher(new TestSender("email")).ProcessAsync(Event("missing-adapter"));
        var pending = await f.Db.Deliveries.SingleAsync(x => x.ChannelId == 2);
        Assert.Equal(DeliveryStatus.Pending, pending.Status);
        Assert.Equal(0, pending.AttemptCount);
        Assert.Equal("sender_unavailable", pending.LastError);
        await f.Dispatcher(new TestSender("slack")).RetryPendingAsync();
        Assert.Equal(DeliveryStatus.Succeeded, pending.Status);
        Assert.Null(pending.LastError);
    }

    [Fact]
    public async Task ControlledDispatchShowsTwoPersistentDeliveriesAndFakeCalls()
    {
        await using var fixture = await Fixture.CreateAsync();
        var email = new FakeChannelSender("email");
        var slack = new FakeChannelSender("slack");
        var dispatcher = fixture.Dispatcher(email, slack);
        Assert.True(await dispatcher.ProcessAsync(Event("demo")));
        await using var reopened = new AlertsIdentityDbContext(fixture.Options);
        var rows = await reopened.Deliveries.OrderBy(x => x.ChannelId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, x =>
        {
            Assert.Equal(DeliveryStatus.Succeeded, x.Status);
            Assert.Equal(1, x.AttemptCount);
            Assert.NotNull(x.CompletedAt);
            Assert.Null(x.LastError);
            output.WriteLine($"delivery={x.Id}, user={x.UserId}, channel={x.ChannelId}, status={x.Status}, attempts={x.AttemptCount}");
        });
        Assert.Single(email.Calls);
        Assert.Single(slack.Calls);
        output.WriteLine($"fake email calls={email.Calls.Count}; fake slack calls={slack.Calls.Count}");
        Assert.Equal("demo", email.Calls[0].ExternalEventId);
    }

    [Theory]
    [InlineData("unrelated", 0)]
    [InlineData("no-topics", 0)]
    [InlineData("no-channels", 0)]
    [InlineData("user", 0)]
    [InlineData("topic", 0)]
    [InlineData("channel", 1)]
    public async Task RoutingHonorsIndependentSelectionsAndEnablement(string change, int expected)
    {
        await using var f = await Fixture.CreateAsync();
        switch (change)
        {
            case "unrelated":
            case "no-topics": f.Db.UserTopicSelections.RemoveRange(f.Db.UserTopicSelections); break;
            case "no-channels": f.Db.UserChannelSelections.RemoveRange(f.Db.UserChannelSelections); break;
            case "user": (await f.Db.Users.FindAsync("alice"))!.IsEnabled = false; break;
            case "topic": (await f.Db.Topics.FindAsync(1))!.IsEnabled = false; break;
            case "channel": (await f.Db.Channels.FindAsync(2))!.IsEnabled = false; break;
        }
        if (change == "unrelated") f.Db.UserTopicSelections.Add(new() { UserId = "alice", TopicId = 3 });
        await f.Db.SaveChangesAsync();
        var sender = new TestSender("email");
        var slack = new TestSender("slack");
        await f.Dispatcher(sender, slack).ProcessAsync(Event("eligibility"));
        Assert.Equal(expected, await f.Db.Deliveries.CountAsync());
        Assert.Equal(expected, sender.Calls + slack.Calls);
    }

    [Fact]
    public async Task RemovingChannelSkipsItForAllTopicsAndDoesNotAffectOtherUsers()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.UserTopicSelections.Add(new() { UserId = "alice", TopicId = 3 });
        f.Db.Users.Add(new() { Id = "bob" });
        f.Db.UserTopicSelections.Add(new() { UserId = "bob", TopicId = 1 });
        f.Db.UserChannelSelections.Add(new() { UserId = "bob", ChannelId = 2 });
        await f.Db.SaveChangesAsync();
        await new SelectionService(f.Db).SetChannelAsync("alice", 2, false);
        var email = new TestSender("email");
        var slack = new TestSender("slack");
        var dispatcher = f.Dispatcher(email, slack);
        await dispatcher.ProcessAsync(Event("finance"));
        await dispatcher.ProcessAsync(Event("weather", 3));
        Assert.Equal(2, email.Calls);
        Assert.Equal(1, slack.Calls);
        Assert.DoesNotContain(f.Db.Deliveries, x => x.UserId == "alice" && x.ChannelId == 2);
    }

    [Fact]
    public async Task DuplicateIdIgnoresChangedPayloadAcrossContextsAndConcurrentCalls()
    {
        await using var f = await Fixture.CreateAsync();
        var sender = new TestSender("email");
        var slack = new TestSender("slack");
        var first = f.Dispatcher(sender, slack);
        await using var otherDb = new AlertsIdentityDbContext(f.Options);
        var second = f.Dispatcher(otherDb, sender, slack);
        var results = await Task.WhenAll(first.ProcessAsync(Event("same")), second.ProcessAsync(Event("same", 3)));
        Assert.Single(results, x => x);
        Assert.False(await second.ProcessAsync(Event("same")));
        Assert.Single(f.Db.AlertEvents);
        Assert.Equal(2, await f.Db.Deliveries.CountAsync());
        Assert.Equal(2, sender.Calls + slack.Calls);
    }

    [Theory]
    [InlineData(2, DeliveryStatus.Succeeded)]
    [InlineData(3, DeliveryStatus.Failed)]
    [InlineData(10, DeliveryStatus.Failed)]
    public async Task RetriesUseExponentialDelaysAndStopAtThree(int failures, DeliveryStatus expected)
    {
        await using var f = await Fixture.CreateAsync();
        var sender = new TestSender("email", _ => throw new InvalidOperationException("secret webhook/password"), failures);
        var slack = new TestSender("slack");
        var dispatcher = f.Dispatcher(sender, slack);
        await dispatcher.ProcessAsync(Event("retry"));
        var delivery = await f.Db.Deliveries.SingleAsync(x => x.ChannelId == 1);
        Assert.Equal(expected, delivery.Status);
        Assert.Equal(3, delivery.AttemptCount);
        Assert.Equal(3, sender.Calls);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, f.Clock.Delays);
        Assert.Equal(expected == DeliveryStatus.Failed ? "send_failed" : null, delivery.LastError);
        Assert.Equal(1, slack.Calls);
        await dispatcher.RetryPendingAsync();
        await dispatcher.ProcessAsync(Event("retry"));
        Assert.Equal(3, sender.Calls);
    }

    [Theory]
    [InlineData("channel")]
    [InlineData("topic")]
    [InlineData("user")]
    [InlineData("selection")]
    public async Task RechecksEligibilityBeforeRetryAndRecoversWhenReenabled(string disable)
    {
        await using var f = await Fixture.CreateAsync();
        var email = new TestSender("email", async _ =>
        {
            await using var other = new AlertsIdentityDbContext(f.Options);
            if (disable == "channel") (await other.Channels.FindAsync(1))!.IsEnabled = false;
            if (disable == "topic") (await other.Topics.FindAsync(1))!.IsEnabled = false;
            if (disable == "user") (await other.Users.FindAsync("alice"))!.IsEnabled = false;
            if (disable == "selection") other.UserChannelSelections.Remove((await other.UserChannelSelections.FindAsync("alice", 1))!);
            await other.SaveChangesAsync();
            throw new Exception("not persisted");
        }, 1);
        var slack = new TestSender("slack");
        await f.Dispatcher(email, slack).ProcessAsync(Event("toggle"));
        Assert.Equal(1, email.Calls);
        await using var resumed = new AlertsIdentityDbContext(f.Options);
        var pending = await resumed.Deliveries.SingleAsync(x => x.ChannelId == 1);
        Assert.Equal(DeliveryStatus.Pending, pending.Status);
        (await resumed.Channels.FindAsync(1))!.IsEnabled = true;
        (await resumed.Topics.FindAsync(1))!.IsEnabled = true;
        (await resumed.Users.FindAsync("alice"))!.IsEnabled = true;
        if (disable == "selection") resumed.UserChannelSelections.Add(new() { UserId = "alice", ChannelId = 1 });
        await resumed.SaveChangesAsync();
        await f.Dispatcher(resumed, email, slack).RetryPendingAsync();
        Assert.Equal(2, email.Calls);
        Assert.Equal(2, pending.AttemptCount);
        Assert.Equal(DeliveryStatus.Succeeded, pending.Status);
    }

    [Theory]
    [InlineData(1, 2, DeliveryStatus.Succeeded)]
    [InlineData(3, 3, DeliveryStatus.Failed)]
    public async Task CancellationAndRestartPreserveReservedAttempts(int reserved, int final, DeliveryStatus expected)
    {
        await using var f = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var email = new TestSender("email", _ => { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            f.Dispatcher(email, new TestSender("slack")).ProcessAsync(Event("interrupted"), cancellation.Token));
        await using var resumed = new AlertsIdentityDbContext(f.Options);
        var pending = await resumed.Deliveries.SingleAsync(x => x.ChannelId == 1);
        Assert.Equal(1, pending.AttemptCount);
        Assert.Equal(DeliveryStatus.Pending, pending.Status);
        // Also simulate interruption during the last reserved attempt.
        pending.AttemptCount = reserved;
        await resumed.SaveChangesAsync();
        var success = new TestSender("email");
        await f.Dispatcher(resumed, success, new TestSender("slack")).RetryPendingAsync();
        Assert.Equal(final, pending.AttemptCount);
        Assert.Equal(expected, pending.Status);
        Assert.Equal(reserved == 3 ? 0 : 1, success.Calls);
    }

    [Fact]
    public async Task InvalidEventsAreRejectedBeforePersistence()
    {
        await using var f = await Fixture.CreateAsync();
        var dispatcher = f.Dispatcher(new TestSender("email"));
        var invalid = new[] { Event(""), Event(new string('x', 257)), Event("unknown", 999), Event("zero", 0) };
        foreach (var alert in invalid)
            await Assert.ThrowsAsync<ArgumentException>(() => dispatcher.ProcessAsync(alert));
        var badTime = Event("time");
        badTime.OccurredAt = default;
        await Assert.ThrowsAsync<ArgumentException>(() => dispatcher.ProcessAsync(badTime));
        Assert.Empty(f.Db.AlertEvents);
        Assert.Empty(f.Db.Deliveries);
    }

    [Fact]
    public async Task OnlyExternalEventUniqueViolationIsIgnored()
    {
        await using var f = await Fixture.CreateAsync();
        var interceptor = new DuplicateInterceptor();
        var options = new DbContextOptionsBuilder<AlertsIdentityDbContext>(f.Options).AddInterceptors(interceptor).Options;
        await using var racing = new AlertsIdentityDbContext(options);
        Assert.False(await f.Dispatcher(racing, new TestSender("email")).ProcessAsync(Event("race")));
        Assert.Empty(racing.ChangeTracker.Entries<AlertEvent>());
        interceptor.Constraint = "IX_Deliveries_EventId_UserId_ChannelId";
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Dispatcher(racing, new TestSender("email")).ProcessAsync(Event("different")));
    }

    private sealed class DuplicateInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public string Constraint { get; set; } = "IX_AlertEvents_ExternalEventId";
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("safe synthetic conflict", new PostgresException("synthetic", "ERROR", "ERROR", "23505", constraintName: Constraint));
    }

    private static AlertEvent Event(string id, int topic = 1) => new()
    {
        ExternalEventId = id,
        TopicId = topic,
        Title = "Controlled sample",
        Body = "Sample content",
        OccurredAt = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)
    };

    private sealed class TestSender(string code, Func<CancellationToken, Task>? action = null, int failures = int.MaxValue) : IChannelSender
    {
        public string ChannelCode => code;
        public int Calls { get; private set; }
        public Task SendAsync(DeliveryMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            return Calls <= failures && action is not null ? action(cancellationToken) : Task.CompletedTask;
        }
    }

    private sealed class AdvancingClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        public List<TimeSpan> Delays { get; } = [];
        public override DateTimeOffset GetUtcNow() => now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            now += dueTime;
            return TimeProvider.System.CreateTimer(callback, state, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public DbContextOptions<AlertsIdentityDbContext> Options { get; } = new DbContextOptionsBuilder<AlertsIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public AlertsIdentityDbContext Db { get; }
        public AdvancingClock Clock { get; } = new();
        private DispatchGate Gate { get; } = new();
        private Fixture() => Db = new(Options);
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            await f.Db.Database.EnsureCreatedAsync();
            f.Db.Users.Add(new() { Id = "alice" });
            f.Db.UserTopicSelections.Add(new() { UserId = "alice", TopicId = 1 });
            f.Db.UserChannelSelections.AddRange(new UserChannelSelection { UserId = "alice", ChannelId = 1 },
                new UserChannelSelection { UserId = "alice", ChannelId = 2 });
            await f.Db.SaveChangesAsync();
            return f;
        }
        public AlertDispatcher Dispatcher(params IChannelSender[] senders) => Dispatcher(Db, senders);
        public AlertDispatcher Dispatcher(AlertsIdentityDbContext db, params IChannelSender[] senders) =>
            new(db, senders, Gate, Clock, NullLogger<AlertDispatcher>.Instance);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); Gate.Dispose(); }
    }
}
