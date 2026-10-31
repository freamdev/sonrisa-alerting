using Alerts.Api.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Alerts.Api.Infrastructure.Identity;

public static class IdentitySetup
{
    public static IServiceCollection AddAlertsIdentity(this IServiceCollection services,
        IConfiguration configuration, bool development)
    {
        services.AddDbContext<AlertsIdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Alerts")
                ?? throw new InvalidOperationException("Configure ConnectionStrings:Alerts for PostgreSQL.")));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        })
            .AddEntityFrameworkStores<AlertsIdentityDbContext>()
            .AddSignInManager<EnabledSignInManager>()
            .AddDefaultTokenProviders();

        services.AddScoped<EnabledCookieEvents>();
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Alerts.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.EventsType = typeof(EnabledCookieEvents);
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "Alerts.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AccessPolicies.User, policy => policy.RequireAuthenticatedUser());
            options.AddPolicy(AccessPolicies.Admin, policy =>
                policy.RequireAuthenticatedUser().RequireRole(AccessPolicies.AdminRole));
        });
        return services;
    }
}
