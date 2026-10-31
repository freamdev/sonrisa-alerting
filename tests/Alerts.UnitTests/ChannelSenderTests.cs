using System.Net;
using System.Text.Json;
using Alerts.Api.Application;
using Alerts.Api.Domain;
using Alerts.Api.Infrastructure;
using Alerts.Api.Infrastructure.Channels;
using Alerts.Api.Infrastructure.Identity;
using Alerts.Api.Infrastructure.News;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Xunit.Abstractions;

namespace Alerts.UnitTests;

public sealed class ChannelSenderTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PollingThroughHttpAndBothAdaptersSurvivesFailureAndIgnoresRepolls()
    {
        using var handler = new ControlledNewsHandler();
        await using var f = await Fixture.CreateAsync(services => services.AddScoped<INewsSource>(provider =>
            new HttpNewsSource(new HttpClient(handler, disposeHandler: false),
                provider.GetRequiredService<AlertsIdentityDbContext>(), Options.Create(new NewsSourceOptions()),
                provider.GetRequiredService<ILogger<HttpNewsSource>>())));
        using var worker = new NewsPollingWorker(f.Provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new NewsSourceOptions { Enabled = true, IntervalSeconds = 1 }), TimeProvider.System,
            f.Provider.GetRequiredService<ILogger<NewsPollingWorker>>());
        await worker.StartAsync(default);
        await handler.CompletedPolls.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
        await using var reopened = new AlertsIdentityDbContext(f.DbOptions);
        Assert.Equal(2, await reopened.AlertEvents.CountAsync());
        var rows = await reopened.Deliveries.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(DeliveryStatus.Succeeded, row.Status);
            Assert.Equal(1, row.AttemptCount);
            output.WriteLine($"delivery={row.Id}, channel={row.ChannelId}, status={row.Status}, attempts={row.AttemptCount}");
        });
        Assert.Single(f.Smtp.Messages);
        Assert.Single(f.Http.Requests);
        Assert.Contains(f.Logs.Entries, x => x.Contains("failed during source"));
        Assert.Contains(f.Logs.Entries, x => x.Contains("0 new events"));
        output.WriteLine("HTTP 503 recovered; two successful fixture polls; duplicates ignored; Email=1, Slack=1; worker stopped cleanly.");
    }

    private sealed class ControlledNewsHandler : HttpMessageHandler
    {
        private int calls;
        public TaskCompletionSource CompletedPolls { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1) return new(HttpStatusCode.ServiceUnavailable);
            if (call == 4)
            {
                CompletedPolls.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "news.json"), ct))
            };
        }
    }

    private static DeliveryMessage Message(string user = "alice", string title = "Sample <&>\r\nBcc: attacker@example.test") =>
        new(1, user, "sample-05", 1, title, "<script>alert(1)</script> & <!channel> *news*", DateTimeOffset.Parse("2026-10-31T12:00:00Z"));

    [Fact]
    public async Task ControlledAdaptersProduceTwoPersistedSuccessesWithLocalClientFakes()
    {
        await using var f = await Fixture.CreateAsync();
        var registered = f.Scope.ServiceProvider.GetServices<IChannelSender>().ToList();
        Assert.IsType<EmailChannelSender>(registered.Single(x => x.ChannelCode == "email"));
        Assert.IsType<SlackChannelSender>(registered.Single(x => x.ChannelCode == "slack"));
        await f.Dispatcher.ProcessAsync(new AlertEvent
        {
            ExternalEventId = "phase05-demo",
            TopicId = 1,
            Title = "Controlled sample",
            Body = "Sample content",
            OccurredAt = DateTimeOffset.Parse("2026-10-31T12:00:00Z")
        });
        await using var reopened = new AlertsIdentityDbContext(f.DbOptions);
        var deliveries = await reopened.Deliveries.OrderBy(x => x.ChannelId).ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, row =>
        {
            Assert.Equal(DeliveryStatus.Succeeded, row.Status);
            Assert.Equal(1, row.AttemptCount);
            Assert.Null(row.LastError);
            output.WriteLine($"delivery={row.Id}, user={row.UserId}, channel={row.ChannelId}, status={row.Status}, attempts={row.AttemptCount}");
        });
        using var email = MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Assert.Single(f.Smtp.Messages))));
        Assert.Equal("alice@example.test", Assert.Single(email.To.Mailboxes).Address);
        Assert.Equal("[Alerts] Controlled sample", email.Subject);
        var request = Assert.Single(f.Http.Requests);
        Assert.Equal("/services/alice-secret", request.Uri.AbsolutePath);
        using var slack = JsonDocument.Parse(request.Body);
        Assert.Contains("Sample content", slack.RootElement.GetProperty("text").GetString());
        output.WriteLine($"SMTP fake: recipient={email.To}, subject={email.Subject}, body={email.TextBody}");
        output.WriteLine($"Slack HTTP fake: POST, payload={request.Body}; destination matched Alice's configured webhook (URL omitted)");
    }

    [Fact]
    public async Task EmailUsesCurrentUsersEmailAndPlainMimeWithoutHeaderInjection()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Sender("email").SendAsync(Message(), default);
        using var mail = MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Assert.Single(f.Smtp.Messages))));
        Assert.Equal("alice@example.test", Assert.Single(mail.To.Mailboxes).Address);
        Assert.Equal("alerts@example.test", Assert.Single(mail.From.Mailboxes).Address);
        Assert.Empty(mail.Bcc);
        Assert.NotNull(mail.Subject);
        Assert.DoesNotContain('\r', mail.Subject);
        Assert.DoesNotContain('\n', mail.Subject);
        Assert.Contains(Message().Body, mail.TextBody);
        Assert.Contains("2026-10-31T12:00:00", mail.TextBody);
        Assert.Null(mail.HtmlBody);

        (await f.Db.Users.FindAsync("alice"))!.Email = "updated@example.test";
        await f.Db.SaveChangesAsync();
        await f.Sender("email").SendAsync(Message(), default);
        Assert.Contains("updated@example.test", f.Smtp.Messages[1]);
    }

    [Theory]
    [InlineData(null, "destination_missing")]
    [InlineData("", "destination_missing")]
    [InlineData("not-an-address", "destination_invalid")]
    [InlineData("alice@example.test\r\nBcc: bob@example.test", "destination_invalid")]
    public async Task EmailRejectsMissingOrInvalidRecipientWithoutCallingTransport(string? address, string code)
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Db.Users.FindAsync("alice"))!.Email = address;
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ChannelDeliveryException>(() => f.Sender("email").SendAsync(Message(), default));
        Assert.Equal(code, error.ErrorCode);
        Assert.Empty(f.Smtp.Messages);
    }

    [Fact]
    public async Task EmailRejectsUnknownUserAndInvalidSenderConfiguration()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.Equal("destination_missing", (await Assert.ThrowsAsync<ChannelDeliveryException>(() =>
            f.Sender("email").SendAsync(Message("absent"), default))).ErrorCode);
        var sender = new EmailChannelSender(f.Db, Options.Create(new EmailOptions { From = "" }), f.Smtp);
        Assert.Equal("configuration_invalid", (await Assert.ThrowsAsync<ChannelDeliveryException>(() =>
            sender.SendAsync(Message(), default))).ErrorCode);
        Assert.Empty(f.Smtp.Messages);
    }

    [Theory]
    [InlineData("", 587, "StartTls", "", "", "Development")]
    [InlineData("smtp", 0, "StartTls", "", "", "Development")]
    [InlineData("smtp", 587, "Auto", "", "", "Development")]
    [InlineData("smtp", 587, "None", "", "", "Production")]
    [InlineData("smtp", 587, "StartTls", "user", "", "Development")]
    [InlineData("smtp", 587, "StartTls", "", "secret", "Development")]
    public async Task SmtpInvalidConfigurationFailsBeforeNetwork(string host, int port, string security, string username, string password, string environment)
    {
        var transport = new MailKitEmailTransport(Options.Create(new EmailOptions
        { Host = host, Port = port, Security = security, Username = username, Password = password }),
            new HostingEnvironment { EnvironmentName = environment });
        using var mail = new MimeMessage();
        Assert.Equal("configuration_invalid", (await Assert.ThrowsAsync<ChannelDeliveryException>(() =>
            transport.SendAsync(mail, default))).ErrorCode);
    }

    [Fact]
    public async Task SlackChoosesPerUserWebhookAndUsesBoundedPlainText()
    {
        await using var f = await Fixture.CreateAsync();
        var sender = f.Sender("slack");
        await sender.SendAsync(Message(), default);
        await sender.SendAsync(Message("bob", new string('x', 4000)), default);
        Assert.EndsWith("/alice-secret", f.Http.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/bob-secret", f.Http.Requests[1].Uri.AbsolutePath);
        using var first = JsonDocument.Parse(f.Http.Requests[0].Body);
        var root = first.RootElement;
        Assert.False(root.GetProperty("mrkdwn").GetBoolean());
        Assert.False(root.GetProperty("unfurl_links").GetBoolean());
        Assert.Equal("none", root.GetProperty("parse").GetString());
        Assert.DoesNotContain("<!channel>", root.GetProperty("text").GetString());
        Assert.Contains("&lt;!channel&gt;", root.GetProperty("text").GetString());
        var block = root.GetProperty("blocks")[0].GetProperty("text");
        Assert.Equal("plain_text", block.GetProperty("type").GetString());
        Assert.Contains(Message().Body, block.GetProperty("text").GetString());
        using var second = JsonDocument.Parse(f.Http.Requests[1].Body);
        Assert.Equal(3000, second.RootElement.GetProperty("text").GetString()!.Length);
    }

    [Theory]
    [InlineData("{}", "destination_missing", "Development", false)]
    [InlineData("bad-json-secret", "configuration_invalid", "Development", false)]
    [InlineData("null", "configuration_invalid", "Development", false)]
    [InlineData("{\"alice\":\"https://example.test/secret\"}", "destination_invalid", "Development", false)]
    [InlineData("{\"alice\":\"http://hooks.slack.com/services/secret\"}", "destination_invalid", "Development", false)]
    [InlineData("{\"alice\":\"https://hooks.slack.com.evil.test/services/secret\"}", "destination_invalid", "Development", false)]
    [InlineData("{\"alice\":\"http://localhost:9999/secret\"}", "destination_invalid", "Development", false)]
    [InlineData("{\"alice\":\"http://localhost:9999/secret\"}", "destination_invalid", "Production", true)]
    [InlineData("{\"alice\":\"https://user:secret@hooks.slack.com/services/secret\"}", "destination_invalid", "Development", false)]
    public async Task SlackRejectsMissingMalformedOrUnsafeConfiguration(string json, string code, string environment, bool allowMock)
    {
        var handler = new FakeHttp();
        using var client = new HttpClient(handler);
        var sender = new SlackChannelSender(client, Options.Create(new SlackOptions { WebhooksJson = json, AllowLocalMock = allowMock }),
            new HostingEnvironment { EnvironmentName = environment });
        var error = await Assert.ThrowsAsync<ChannelDeliveryException>(() => sender.SendAsync(Message(), default));
        Assert.Equal(code, error.ErrorCode);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("secret", error.ToString());
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SlackExplicitDevelopmentLocalMockIsAccepted()
    {
        var handler = new FakeHttp();
        using var client = new HttpClient(handler);
        var sender = new SlackChannelSender(client, Options.Create(new SlackOptions
        { AllowLocalMock = true, WebhooksJson = "{\"alice\":\"http://slack-mock:8080/services/local\"}" }),
            new HostingEnvironment { EnvironmentName = "Development" });
        await sender.SendAsync(Message(), default);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task SlackProviderFailuresAreSanitized(HttpStatusCode status)
    {
        await using var f = await Fixture.CreateAsync();
        f.Http.Status = status;
        var error = await Assert.ThrowsAsync<ChannelDeliveryException>(() => f.Sender("slack").SendAsync(Message(), default));
        Assert.Equal("provider_rejected", error.ErrorCode);
        Assert.DoesNotContain("secret", error.ToString());
    }

    [Fact]
    public async Task ProviderErrorsPersistSafeCodesAndStopAtThreeWhileOtherChannelSucceeds()
    {
        await using var f = await Fixture.CreateAsync();
        f.Smtp.Failure = new IOException("secret password or destination");
        await f.Dispatcher.ProcessAsync(new AlertEvent
        { ExternalEventId = "failure", TopicId = 1, Title = "Sample", Body = "Content", OccurredAt = DateTimeOffset.UtcNow });
        var row = await f.Db.Deliveries.SingleAsync(x => x.ChannelId == 1);
        Assert.Equal(DeliveryStatus.Failed, row.Status);
        Assert.Equal(3, row.AttemptCount);
        Assert.Equal("provider_unavailable", row.LastError);
        Assert.Equal(3, f.Smtp.Messages.Count);
        Assert.Equal(DeliveryStatus.Succeeded, (await f.Db.Deliveries.SingleAsync(x => x.ChannelId == 2)).Status);
        await f.Dispatcher.RetryPendingAsync();
        Assert.Equal(3, f.Smtp.Messages.Count);
        Assert.DoesNotContain(f.Logs.Entries, x => x.Contains("secret") || x.Contains("hooks.slack.com"));
    }

    [Fact]
    public async Task SmtpRejectionBecomesSafeProviderRejectedCode()
    {
        await using var f = await Fixture.CreateAsync();
        f.Smtp.Failure = new MailKit.Net.Smtp.SmtpCommandException(MailKit.Net.Smtp.SmtpErrorCode.RecipientNotAccepted,
            MailKit.Net.Smtp.SmtpStatusCode.MailboxUnavailable, "secret recipient and provider details");
        var error = await Assert.ThrowsAsync<ChannelDeliveryException>(() => f.Sender("email").SendAsync(Message(), default));
        Assert.Equal("provider_rejected", error.ErrorCode);
        Assert.DoesNotContain("secret", error.ToString());
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("slack")]
    public async Task MissingDestinationUsesExistingThreeAttemptPolicy(string channel)
    {
        await using var f = await Fixture.CreateAsync();
        if (channel == "email") (await f.Db.Users.FindAsync("alice"))!.Email = null;
        else f.Provider.GetRequiredService<IOptions<SlackOptions>>().Value.WebhooksJson = "{}";
        await f.Db.SaveChangesAsync();
        await f.Dispatcher.ProcessAsync(new AlertEvent
        { ExternalEventId = "missing", TopicId = 1, Title = "Sample", Body = "", OccurredAt = DateTimeOffset.UtcNow });
        var row = await f.Db.Deliveries.SingleAsync(x => x.ChannelId == (channel == "email" ? 1 : 2));
        Assert.Equal(DeliveryStatus.Failed, row.Status);
        Assert.Equal(3, row.AttemptCount);
        Assert.Equal("destination_missing", row.LastError);
        if (channel == "email") Assert.Empty(f.Smtp.Messages);
        else Assert.Empty(f.Http.Requests);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("slack")]
    public async Task CallerCancellationPropagatesButProviderTimeoutIsSanitized(string channel)
    {
        await using var f = await Fixture.CreateAsync();
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Sender(channel).SendAsync(Message(), ct.Token));
        Assert.Empty(f.Http.Requests);
        Assert.Empty(f.Smtp.Messages);
        f.Smtp.Failure = new TaskCanceledException("secret");
        f.Http.Failure = new TaskCanceledException("secret");
        Assert.Equal("provider_unavailable", (await Assert.ThrowsAsync<ChannelDeliveryException>(() =>
            f.Sender(channel).SendAsync(Message(), default))).ErrorCode);
    }

    [Fact]
    public async Task SharedSlackDestinationRetainsSeparateUserDeliveries()
    {
        await using var f = await Fixture.CreateAsync();
        f.Provider.GetRequiredService<IOptions<SlackOptions>>().Value.WebhooksJson =
            "{\"alice\":\"https://hooks.slack.com/services/shared-secret\",\"bob\":\"https://hooks.slack.com/services/shared-secret\"}";
        f.Db.Users.Add(new() { Id = "bob", Email = "bob@example.test" });
        f.Db.UserTopicSelections.Add(new() { UserId = "bob", TopicId = 1 });
        f.Db.UserChannelSelections.Add(new() { UserId = "bob", ChannelId = 2 });
        await f.Db.SaveChangesAsync();
        await f.Dispatcher.ProcessAsync(new AlertEvent
        { ExternalEventId = "shared", TopicId = 1, Title = "Sample", Body = "", OccurredAt = DateTimeOffset.UtcNow });
        Assert.Equal(2, f.Http.Requests.Count);
        Assert.Equal(f.Http.Requests[0].Uri, f.Http.Requests[1].Uri);
        Assert.Equal(2, await f.Db.Deliveries.CountAsync(x => x.ChannelId == 2 && x.Status == DeliveryStatus.Succeeded));
    }

    private sealed class FakeSmtp : IEmailTransport
    {
        public List<string> Messages { get; } = [];
        public Exception? Failure { get; set; }
        public Task SendAsync(MimeMessage message, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Messages.Add(message.ToString());
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class FakeHttp : HttpMessageHandler
    {
        public List<(Uri Uri, string Body)> Requests { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public Exception? Failure { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!, await request.Content!.ReadAsStringAsync(ct)));
            if (Failure is not null) throw Failure;
            return new HttpResponseMessage(Status) { Content = new StringContent("secret response body") };
        }
    }

    private sealed class LogCapture : ILoggerProvider
    {
        public List<string> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
        public void Dispose() { }
        private sealed class CaptureLogger(LogCapture owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Entries.Add(formatter(state, exception) + exception);
        }
    }

    private sealed class AdvancingClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            now += dueTime;
            return System.CreateTimer(callback, state, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public DbContextOptions<AlertsIdentityDbContext> DbOptions { get; } =
            new DbContextOptionsBuilder<AlertsIdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public FakeSmtp Smtp { get; } = new();
        public FakeHttp Http { get; } = new();
        public LogCapture Logs { get; } = new();
        public ServiceProvider Provider { get; }
        public AsyncServiceScope Scope { get; }
        public AlertsIdentityDbContext Db => Scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>();
        public IAlertDispatcher Dispatcher => Scope.ServiceProvider.GetRequiredService<IAlertDispatcher>();
        public IChannelSender Sender(string code) => Scope.ServiceProvider.GetServices<IChannelSender>().Single(x => x.ChannelCode == code);
        private Fixture(Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
            services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = "Development" });
            services.AddScoped(_ => new AlertsIdentityDbContext(DbOptions));
            services.AddAlertRouting(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Channels:Email:From"] = "alerts@example.test",
                ["Channels:Slack:WebhooksJson"] = "{\"alice\":\"https://hooks.slack.com/services/alice-secret\",\"bob\":\"https://hooks.slack.com/services/bob-secret\"}"
            }).Build());
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new AdvancingClock());
            services.RemoveAll<IEmailTransport>();
            services.AddSingleton<IEmailTransport>(Smtp);
            services.AddHttpClient<SlackChannelSender>().ConfigurePrimaryHttpMessageHandler(() => Http);
            configure?.Invoke(services);
            Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            Scope = Provider.CreateAsyncScope();
        }
        public static async Task<Fixture> CreateAsync(Action<IServiceCollection>? configure = null)
        {
            var f = new Fixture(configure);
            await f.Db.Database.EnsureCreatedAsync();
            f.Db.Users.Add(new() { Id = "alice", Email = "alice@example.test" });
            f.Db.UserTopicSelections.Add(new() { UserId = "alice", TopicId = 1 });
            f.Db.UserChannelSelections.AddRange(new UserChannelSelection { UserId = "alice", ChannelId = 1 },
                new UserChannelSelection { UserId = "alice", ChannelId = 2 });
            await f.Db.SaveChangesAsync();
            return f;
        }
        public async ValueTask DisposeAsync() { await Scope.DisposeAsync(); await Provider.DisposeAsync(); }
    }
}
