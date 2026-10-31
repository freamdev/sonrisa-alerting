using System.Net;
using System.Net.Http.Json;
using Alerts.Api;
using Alerts.Api.Application;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Alerts.UnitTests;

// In-process HTTP component tests use only an isolated EF test store. No network/real providers.
public sealed class AuthTests
{
    [Fact]
    public async Task AnonymousRequestsAreDenied()
    {
        await using var host = await AuthHost.CreateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Get, "/api/admin/probe")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.SendAsync(HttpMethod.Post, "/api/auth/register")).StatusCode);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Forbidden)]
    [InlineData(true, HttpStatusCode.OK)]
    public async Task RolesEnforceAdminBoundaryAndBothHaveUserAccess(bool admin, HttpStatusCode adminStatus)
    {
        await using var host = await AuthHost.CreateAsync(admin: admin);
        Assert.Equal(HttpStatusCode.NoContent, (await host.LoginAsync()).StatusCode);
        var me = await host.SendAsync(HttpMethod.Get, "/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var session = await me.Content.ReadFromJsonAsync<Session>();
        Assert.Equal(admin, session!.IsAdmin);
        Assert.Equal(AuthHost.Email, session.Email);
        Assert.Equal(adminStatus, (await host.SendAsync(HttpMethod.Get, "/api/admin/probe")).StatusCode);
        var sessionCookie = host.SessionCookie!;
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisabledAccountCannotLoginOrReuseAnExistingSession()
    {
        await using var host = await AuthHost.CreateAsync(admin: true);
        Assert.Equal(HttpStatusCode.NoContent, (await host.LoginAsync()).StatusCode);
        await host.ChangeUserAsync(user => user.IsEnabled = false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Get, "/api/admin/probe")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.LoginAsync()).StatusCode);
    }

    [Fact]
    public async Task DeletedAccountAndChangedSecurityStampInvalidateCookies()
    {
        await using var host = await AuthHost.CreateAsync();
        await host.LoginAsync();
        await host.WithUsersAsync(async (users, user) => Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Get, "/api/auth/me")).StatusCode);
        await host.LoginAsync();
        await host.WithUsersAsync(async (users, user) => Assert.True((await users.DeleteAsync(user)).Succeeded));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Get, "/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task RemovedAdminRoleTakesEffectOnNextRequest()
    {
        await using var host = await AuthHost.CreateAsync(admin: true);
        await host.LoginAsync();
        await host.WithUsersAsync(async (users, user) =>
            Assert.True((await users.RemoveFromRoleAsync(user, AccessPolicies.AdminRole)).Succeeded));
        Assert.Equal(HttpStatusCode.Forbidden, (await host.SendAsync(HttpMethod.Get, "/api/admin/probe")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(HttpMethod.Get, "/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task LoginAndLogoutRequireValidCsrfAndLogoutClearsSession()
    {
        await using var host = await AuthHost.CreateAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.SendAsync(HttpMethod.Post, "/api/auth/login",
            new { email = AuthHost.Email, password = host.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.SendAsync(HttpMethod.Post, "/api/auth/login",
            new { email = AuthHost.Email, password = host.Password }, "invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.LoginAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.SendAsync(HttpMethod.Post, "/api/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.SendAsync(HttpMethod.Post, "/api/auth/logout", token: await host.CsrfAsync())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Get, "/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task WrongPasswordAndUnknownAccountReturnGenericDenialAndFailuresLockOut()
    {
        await using var host = await AuthHost.CreateAsync();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.LoginAsync("incorrect")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.LoginAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.SendAsync(HttpMethod.Post, "/api/auth/login",
            new { email = "unknown@example.test", password = host.Password }, await host.CsrfAsync())).StatusCode);
    }

    private sealed record Session(string Email, bool IsAdmin);
}

internal sealed class AuthHost(WebApplication app, HttpClient client, string password) : IAsyncDisposable
{
    public const string Email = "test-user@example.test";
    private readonly Dictionary<string, string> cookies = [];
    public string Password { get; } = password;
    public string? SessionCookie { get; private set; }
    public IServiceProvider Services => app.Services;
    public IConfiguration Configuration => app.Configuration;
    public IWebHostEnvironment Environment => app.Environment;

    public static async Task<AuthHost> CreateAsync(bool admin = false, string environment = "Development", bool seedCatalog = false)
    {
        var database = Guid.NewGuid().ToString();
        var app = Program.BuildApplication(["--environment", environment], builder =>
        {
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            builder.Services.RemoveAll<DbContextOptions<AlertsIdentityDbContext>>();
            builder.Services.RemoveAll<IDbContextOptionsConfiguration<AlertsIdentityDbContext>>();
            builder.Services.AddDbContext<AlertsIdentityDbContext>(options => options.UseInMemoryDatabase(database));
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
        await app.StartAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        var host = new AuthHost(app, client, $"Test!aA1{Guid.NewGuid():N}");
        using var scope = app.Services.CreateScope();
        if (seedCatalog)
            await scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>().Database.EnsureCreatedAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.CreateAsync(new ApplicationUser { UserName = Email, Email = Email }, host.Password)).Succeeded);
        if (admin)
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(AccessPolicies.AdminRole))).Succeeded);
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(Email))!, AccessPolicies.AdminRole)).Succeeded);
        }
        return host;
    }

    public async Task WithUsersAsync(Func<UserManager<ApplicationUser>, ApplicationUser, Task> action)
    {
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await action(users, (await users.FindByEmailAsync(Email))!);
    }

    public Task ChangeUserAsync(Action<ApplicationUser> change) => WithUsersAsync(async (users, user) =>
    {
        change(user);
        Assert.True((await users.UpdateAsync(user)).Succeeded);
    });

    public async Task<string> CsrfAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Csrf>())!.Token;
    }

    public async Task<HttpResponseMessage> LoginAsync(string? password = null) =>
        await SendAsync(HttpMethod.Post, "/api/auth/login", new { email = Email, password = password ?? Password }, await CsrfAsync());

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (token is not null) request.Headers.Add("X-CSRF-TOKEN", token);
        if (cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}")));
        var response = await client.SendAsync(request);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            foreach (var value in values)
            {
                var pair = value.Split(';', 2)[0].Split('=', 2);
                cookies[pair[0]] = pair[1];
                if (pair[0] == "Alerts.Session") SessionCookie = value;
            }
        }
        return response;
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
    }

    private sealed record Csrf(string Token);
}
