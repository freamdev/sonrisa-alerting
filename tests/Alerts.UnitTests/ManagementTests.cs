using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Alerts.Api;
using Alerts.Api.Application;
using Alerts.Api.Domain;
using Alerts.Api.Infrastructure;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Alerts.UnitTests;

public sealed class ManagementTests
{
    public static TheoryData<string, string> ProtectedRoutes => new()
    {
        { "GET", "/api/me/topics" }, { "GET", "/api/me/channels" }, { "GET", "/api/me/selections" },
        { "PUT", "/api/me/topics/1" }, { "DELETE", "/api/me/topics/1" },
        { "PUT", "/api/me/channels/1" }, { "DELETE", "/api/me/channels/1" },
        { "GET", "/api/admin/users" }, { "GET", "/api/admin/topics" }, { "GET", "/api/admin/channels" },
        { "PUT", "/api/admin/users/other/enabled" }, { "DELETE", "/api/admin/users/other" },
        { "PUT", "/api/admin/topics/1/enabled" }, { "PUT", "/api/admin/channels/1/enabled" }
    };

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task EveryManagementRouteRequiresAuthentication(string method, string path)
    {
        await using var host = await AuthHost.CreateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(new(method), path)).StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/admin/users")]
    [InlineData("GET", "/api/admin/topics")]
    [InlineData("GET", "/api/admin/channels")]
    [InlineData("PUT", "/api/admin/users/other/enabled")]
    [InlineData("DELETE", "/api/admin/users/other")]
    [InlineData("PUT", "/api/admin/topics/1/enabled")]
    [InlineData("PUT", "/api/admin/channels/1/enabled")]
    public async Task EveryAdminRouteRejectsRegularUsers(string method, string path)
    {
        await using var host = await AuthHost.CreateAsync();
        await host.LoginAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await host.SendAsync(new(method), path,
            new { isEnabled = false }, await host.CsrfAsync())).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersonalSelectionsAreIndependentIdempotentAndBoundToSession(bool admin)
    {
        await using var host = await CreateAsync(admin);
        await host.LoginAsync();
        // Client-supplied userId in either query or ignored body cannot select for another account.
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendAsync(HttpMethod.Put,
            "/api/me/topics/1?userId=other", new { userId = "other" }, await host.CsrfAsync())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendAsync(HttpMethod.Put,
            "/api/me/topics/1", token: await host.CsrfAsync())).StatusCode);
        var selected = await SelectionsAsync(host);
        Assert.Equal(new[] { 1 }, selected.TopicIds);
        Assert.Empty(selected.ChannelIds);
        await MutateAsync(host, HttpMethod.Put, "/api/me/channels/2");
        await MutateAsync(host, HttpMethod.Put, "/api/me/channels/2");
        await MutateAsync(host, HttpMethod.Put, "/api/me/topics/3");
        await MutateAsync(host, HttpMethod.Delete, "/api/me/topics/1");
        await MutateAsync(host, HttpMethod.Delete, "/api/me/topics/1");
        selected = await SelectionsAsync(host);
        Assert.Equal(new[] { 3 }, selected.TopicIds);
        Assert.Equal(new[] { 2 }, selected.ChannelIds);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>();
            Assert.DoesNotContain(db.UserTopicSelections, x => x.UserId == "other");
            Assert.DoesNotContain(db.UserChannelSelections, x => x.UserId == "other");
        }
        Assert.Equal(HttpStatusCode.NotFound, (await host.SendAsync(HttpMethod.Put,
            "/api/users/other/topics/1", token: await host.CsrfAsync())).StatusCode);
        await MutateAsync(host, HttpMethod.Delete, "/api/me/channels/2");
        Assert.Empty((await SelectionsAsync(host)).ChannelIds);
        Assert.Equal(new[] { 3 }, (await SelectionsAsync(host)).TopicIds);
    }

    [Theory]
    [InlineData("topics")]
    [InlineData("channels")]
    public async Task DisabledItemsStayVisiblePreserveSelectionsAndRejectOnlyNewAdds(string catalog)
    {
        await using var host = await CreateAsync(true);
        await host.LoginAsync();
        await MutateAsync(host, HttpMethod.Put, $"/api/me/{catalog}/1");
        await MutateAsync(host, HttpMethod.Put, $"/api/admin/{catalog}/1/enabled", new { isEnabled = false });
        var response = await host.SendAsync(HttpMethod.Get, $"/api/me/{catalog}");
        Assert.True(response.Headers.CacheControl!.NoStore);
        var items = await response.Content.ReadFromJsonAsync<CatalogItem[]>();
        Assert.False(items!.Single(x => x.Id == 1).IsEnabled);
        var selections = await SelectionsAsync(host);
        Assert.Equal(new[] { 1 }, catalog == "topics" ? selections.TopicIds : selections.ChannelIds);
        // Repeating an existing selection is an idempotent success even while disabled.
        await MutateAsync(host, HttpMethod.Put, $"/api/me/{catalog}/1");
        await MutateAsync(host, HttpMethod.Delete, $"/api/me/{catalog}/1");
        var rejected = await host.SendAsync(HttpMethod.Put, $"/api/me/{catalog}/1", token: await host.CsrfAsync());
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("item_disabled", (await rejected.Content.ReadFromJsonAsync<ApiError>())!.Error);
        await MutateAsync(host, HttpMethod.Put, $"/api/admin/{catalog}/1/enabled", new { isEnabled = true });
        selections = await SelectionsAsync(host);
        Assert.Empty(catalog == "topics" ? selections.TopicIds : selections.ChannelIds);
        await MutateAsync(host, HttpMethod.Put, $"/api/me/{catalog}/1");
    }

    [Theory]
    [InlineData("PUT", "/api/me/topics/1")]
    [InlineData("DELETE", "/api/me/topics/1")]
    [InlineData("PUT", "/api/me/channels/1")]
    [InlineData("DELETE", "/api/me/channels/1")]
    [InlineData("PUT", "/api/admin/users/other/enabled")]
    [InlineData("DELETE", "/api/admin/users/other")]
    [InlineData("PUT", "/api/admin/topics/1/enabled")]
    [InlineData("PUT", "/api/admin/channels/1/enabled")]
    public async Task EveryMutationRequiresCsrf(string method, string path)
    {
        await using var host = await CreateAsync(true);
        await host.LoginAsync();
        foreach (var token in new string?[] { null, "invalid" })
            Assert.Equal(HttpStatusCode.BadRequest, (await host.SendAsync(new(method), path,
                new { isEnabled = false }, token)).StatusCode);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>();
        Assert.True((await db.Users.FindAsync("other"))!.IsEnabled);
        Assert.True((await db.Topics.FindAsync(1))!.IsEnabled);
        Assert.True((await db.Channels.FindAsync(1))!.IsEnabled);
        Assert.Empty(db.UserTopicSelections);
        Assert.Empty(db.UserChannelSelections);
    }

    [Theory]
    [InlineData("PUT", "/api/me/topics/0", HttpStatusCode.BadRequest)]
    [InlineData("DELETE", "/api/me/channels/-1", HttpStatusCode.BadRequest)]
    [InlineData("PUT", "/api/me/topics/nonsense", HttpStatusCode.BadRequest)]
    [InlineData("PUT", "/api/me/channels/999", HttpStatusCode.NotFound)]
    [InlineData("DELETE", "/api/me/topics/999", HttpStatusCode.NotFound)]
    [InlineData("PUT", "/api/admin/topics/999/enabled", HttpStatusCode.NotFound)]
    [InlineData("PUT", "/api/admin/channels/999/enabled", HttpStatusCode.NotFound)]
    [InlineData("PUT", "/api/admin/users/missing/enabled", HttpStatusCode.NotFound)]
    [InlineData("DELETE", "/api/admin/users/missing", HttpStatusCode.NotFound)]
    public async Task InvalidAndMissingIdsHavePredictableStatuses(string method, string path, HttpStatusCode status)
    {
        await using var host = await CreateAsync(true);
        await host.LoginAsync();
        Assert.Equal(status, (await host.SendAsync(new(method), path, new { isEnabled = false }, await host.CsrfAsync())).StatusCode);
    }

    [Theory]
    [InlineData("topics/1")]
    [InlineData("channels/1")]
    [InlineData("users/other")]
    public async Task EnablementRequiresExplicitBoolean(string resource)
    {
        await using var host = await CreateAsync(true);
        await host.LoginAsync();
        foreach (var body in new object[] { new { }, new { isEnabled = (bool?)null }, new { isEnabled = "false" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await host.SendAsync(HttpMethod.Put,
                $"/api/admin/{resource}/enabled", body, await host.CsrfAsync())).StatusCode);
    }

    [Fact]
    public async Task AdminCannotDisableOrDeleteSelfAndUserListHasOnlySafeFields()
    {
        await using var host = await CreateAsync(true);
        await host.LoginAsync();
        var response = await host.SendAsync(HttpMethod.Get, "/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.All(json.EnumerateArray(), item => Assert.Equal(new[] { "email", "id", "isEnabled" },
            item.EnumerateObject().Select(x => x.Name).OrderBy(x => x)));
        var self = json.EnumerateArray().Single(x => x.GetProperty("email").GetString() == AuthHost.Email).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await host.SendAsync(HttpMethod.Put,
            $"/api/admin/users/{self}/enabled", new { isEnabled = false }, await host.CsrfAsync())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.SendAsync(HttpMethod.Delete,
            $"/api/admin/users/{self}", token: await host.CsrfAsync())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(HttpMethod.Get, "/api/me/selections")).StatusCode);
    }

    [Fact]
    public async Task AdminDisableAndDeleteInvalidateExistingUserSessionAndLogin()
    {
        await using var host = await CreateAsync(true);
        // Preserve a second account's real cookie session against the same host/store.
        var password = "Other!aA1" + Guid.NewGuid().ToString("N");
        await host.WithUsersAsync(async (users, _) =>
            Assert.True((await users.CreateAsync(new ApplicationUser { Id = "victim", UserName = "victim@example.test", Email = "victim@example.test" }, password)).Succeeded));
        await host.SendAsync(HttpMethod.Post, "/api/auth/login", new { email = "victim@example.test", password }, await host.CsrfAsync());
        var victimCookie = host.SessionCookie!.Split(';', 2)[0];
        await host.LoginAsync();
        await MutateAsync(host, HttpMethod.Put, "/api/admin/users/victim/enabled", new { isEnabled = false });
        using var client = new HttpClient(((Microsoft.AspNetCore.TestHost.TestServer)host.Services
            .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()).CreateHandler())
        { BaseAddress = new Uri("https://localhost") };
        client.DefaultRequestHeaders.Add("Cookie", victimCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/selections")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Post, "/api/auth/login",
            new { email = "victim@example.test", password }, await host.CsrfAsync())).StatusCode);
        await MutateAsync(host, HttpMethod.Put, "/api/admin/users/victim/enabled", new { isEnabled = true });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me/selections")).StatusCode);
        await MutateAsync(host, HttpMethod.Delete, "/api/admin/users/victim");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/selections")).StatusCode);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>();
        Assert.False(await db.Users.AnyAsync(x => x.Id == "victim"));
        Assert.Equal(3, await db.Topics.CountAsync());
        Assert.Equal(2, await db.Channels.CountAsync());
        // InMemory cannot prove relational cascades: assert the exact model contract here;
        // the Phase 07 PostgreSQL manual validation checks real rows through HTTP deletion.
        foreach (var type in new[] { typeof(UserTopicSelection), typeof(UserChannelSelection), typeof(Delivery) })
            Assert.Equal(DeleteBehavior.Cascade, db.Model.FindEntityType(type)!.GetForeignKeys()
                .Single(x => x.PrincipalEntityType.ClrType == typeof(ApplicationUser)).DeleteBehavior);
    }

    [Theory]
    [InlineData("topics/1")]
    [InlineData("channels/1")]
    [InlineData("users/other")]
    public async Task ApiGlobalDisableSuppressesDispatchAndReenablePreservesPreferences(string resource)
    {
        await using var host = await CreateAsync(true);
        await host.LoginAsync();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>();
            db.UserTopicSelections.Add(new() { UserId = "other", TopicId = 1 });
            db.UserChannelSelections.Add(new() { UserId = "other", ChannelId = 1 });
            await db.SaveChangesAsync();
        }
        await MutateAsync(host, HttpMethod.Put, $"/api/admin/{resource}/enabled", new { isEnabled = false });
        await DispatchAsync(host, "disabled");
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>();
            Assert.Empty(db.Deliveries);
            Assert.Single(db.UserTopicSelections);
            Assert.Single(db.UserChannelSelections);
        }
        await MutateAsync(host, HttpMethod.Put, $"/api/admin/{resource}/enabled", new { isEnabled = true });
        await DispatchAsync(host, "enabled");
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>();
            Assert.Single(db.Deliveries);
            Assert.Equal(DeliveryStatus.Succeeded, (await db.Deliveries.SingleAsync()).Status);
        }
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    [InlineData("Staging", HttpStatusCode.NotFound)]
    public async Task OpenApiIsDevelopmentOnlyAndDocumentsManagement(string environment, HttpStatusCode status)
    {
        await using var host = await AuthHost.CreateAsync(environment: environment);
        var response = await host.SendAsync(HttpMethod.Get, "/openapi/v1.json");
        Assert.Equal(status, response.StatusCode);
        if (status != HttpStatusCode.OK) return;
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/me/selections", out _));
        var operation = paths.GetProperty("/api/admin/users/{id}").GetProperty("delete");
        Assert.Contains("self-delete", operation.GetProperty("summary").GetString());
        Assert.Contains("X-CSRF-TOKEN", operation.GetProperty("description").GetString());
    }

    private static async Task<AuthHost> CreateAsync(bool admin = false)
    {
        var host = await AuthHost.CreateAsync(admin, seedCatalog: true);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>();
        db.Users.Add(new() { Id = "other", UserName = "other@example.test", Email = "other@example.test" });
        await db.SaveChangesAsync();
        return host;
    }

    private static async Task<UserSelections> SelectionsAsync(AuthHost host) =>
        (await (await host.SendAsync(HttpMethod.Get, "/api/me/selections")).Content.ReadFromJsonAsync<UserSelections>())!;

    private static async Task MutateAsync(AuthHost host, HttpMethod method, string path, object? body = null) =>
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendAsync(method, path, body, await host.CsrfAsync())).StatusCode);

    private static async Task DispatchAsync(AuthHost host, string id)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>();
        var dispatcher = new AlertDispatcher(db, [new FakeChannelSender("email")],
            scope.ServiceProvider.GetRequiredService<DispatchGate>(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AlertDispatcher>.Instance);
        await dispatcher.ProcessAsync(new() { ExternalEventId = id, TopicId = 1, Title = "Sample", Body = "Sample", OccurredAt = DateTimeOffset.UtcNow });
    }
}
