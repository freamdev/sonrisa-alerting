using Alerts.Api.Application;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Alerts.UnitTests;

public sealed class DevelopmentSeedTests
{
    [Fact]
    public async Task SeedIsRepeatableAndDoesNotResetPasswordsOrEnableAccounts()
    {
        await using var host = await AuthHost.CreateAsync();
        var settings = Settings(host.Password);
        await DevelopmentAccountSeeder.SeedAsync(host.Services, settings, host.Environment);
        await host.ChangeUserAsync(user => user.IsEnabled = false);
        settings["DevelopmentSeed:UserPassword"] = $"Changed!aA1{Guid.NewGuid():N}";
        await DevelopmentAccountSeeder.SeedAsync(host.Services, settings, host.Environment);
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(2, await users.Users.CountAsync());
        var admin = (await users.FindByEmailAsync("admin@example.test"))!;
        Assert.True(await users.IsInRoleAsync(admin, AccessPolicies.AdminRole));
        var user = (await users.FindByEmailAsync(AuthHost.Email))!;
        Assert.False(user.IsEnabled);
        Assert.False(await users.IsInRoleAsync(user, AccessPolicies.AdminRole));
        Assert.True(await users.CheckPasswordAsync(user, host.Password));
    }

    [Fact]
    public async Task SeedCannotRunOutsideDevelopment()
    {
        await using var host = await AuthHost.CreateAsync(environment: "Production");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentAccountSeeder.SeedAsync(host.Services, Settings(host.Password), host.Environment));
    }

    [Fact]
    public async Task SeedRejectsMissingCredentialsAndNeverPromotesAnExistingRegularAccount()
    {
        await using var host = await AuthHost.CreateAsync();
        var settings = Settings(host.Password);
        settings["DevelopmentSeed:AdminPassword"] = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentAccountSeeder.SeedAsync(host.Services, settings, host.Environment));
        settings = Settings(host.Password);
        settings["DevelopmentSeed:AdminEmail"] = AuthHost.Email;
        settings["DevelopmentSeed:UserEmail"] = "other@example.test";
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentAccountSeeder.SeedAsync(host.Services, settings, host.Environment));
        await host.WithUsersAsync(async (users, user) =>
            Assert.False(await users.IsInRoleAsync(user, AccessPolicies.AdminRole)));
    }

    private static IConfigurationRoot Settings(string password) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentSeed:Enabled"] = "true",
            ["DevelopmentSeed:AdminEmail"] = "admin@example.test",
            ["DevelopmentSeed:AdminPassword"] = password,
            ["DevelopmentSeed:UserEmail"] = AuthHost.Email,
            ["DevelopmentSeed:UserPassword"] = password
        }).Build();
}
