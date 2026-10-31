using System.Net;
using Alerts.Api.Infrastructure.Identity;

namespace Alerts.UnitTests;

public sealed class RuntimeTests
{
    [Fact]
    public async Task AnonymousHealthReturnsLivenessWithoutAccountDetails()
    {
        await using var host = await AuthHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Get, "/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store, no-cache", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task MigrationCommandRejectsNonDevelopmentBeforeAccessingDatabase()
    {
        await using var host = await AuthHost.CreateAsync(environment: "Production");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentDatabaseMigrator.MigrateAsync(host.Services, host.Environment));
        Assert.Equal("Development migrations are allowed only in Development.", error.Message);
    }
}
