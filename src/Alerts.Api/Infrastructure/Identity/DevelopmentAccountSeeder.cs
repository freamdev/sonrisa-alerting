using Alerts.Api.Application;
using Microsoft.AspNetCore.Identity;

namespace Alerts.Api.Infrastructure.Identity;

public static class DevelopmentAccountSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
        {
            return;
        }
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Development account seeding is allowed only in Development.");
        }

        // Validate all inputs before writing anything. Never put supplied values in errors/logs.
        var adminEmail = Required(configuration, "AdminEmail");
        var adminPassword = Required(configuration, "AdminPassword");
        var userEmail = Required(configuration, "UserEmail");
        var userPassword = Required(configuration, "UserPassword");
        if (string.Equals(adminEmail, userEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Development admin and user must have different email addresses.");
        }

        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (!await roles.RoleExistsAsync(AccessPolicies.AdminRole))
        {
            Ensure(await roles.CreateAsync(new IdentityRole(AccessPolicies.AdminRole)));
        }

        await CreateAsync(users, adminEmail, adminPassword, admin: true);
        await CreateAsync(users, userEmail, userPassword, admin: false);
    }

    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[$"DevelopmentSeed:{key}"])
            ? configuration[$"DevelopmentSeed:{key}"]!
            : throw new InvalidOperationException($"Configure DevelopmentSeed:{key} before enabling seeding.");

    private static async Task CreateAsync(UserManager<ApplicationUser> users, string email,
        string password, bool admin)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null)
        {
            // Reruns never reset passwords, re-enable accounts, or promote existing accounts.
            if (await users.IsInRoleAsync(user, AccessPolicies.AdminRole) != admin)
            {
                throw new InvalidOperationException("An existing development account has an unexpected role. Review seed configuration.");
            }
            return;
        }

        user = new ApplicationUser { UserName = email, Email = email };
        Ensure(await users.CreateAsync(user, password));
        if (admin)
        {
            Ensure(await users.AddToRoleAsync(user, AccessPolicies.AdminRole));
        }
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Development account bootstrap failed. Check configuration and Identity password requirements.");
        }
    }
}
