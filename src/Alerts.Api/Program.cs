using Alerts.Api.Infrastructure;
using Alerts.Api.Infrastructure.Identity;

namespace Alerts.Api;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var migrate = args.Contains("--migrate-development", StringComparer.Ordinal);
        await using var app = BuildApplication(args.Where(arg => arg != "--migrate-development").ToArray());
        try
        {
            if (migrate)
            {
                await DevelopmentDatabaseMigrator.MigrateAsync(app.Services, app.Environment);
                return 0;
            }
            await DevelopmentAccountSeeder.SeedAsync(app.Services, app.Configuration, app.Environment);
            app.Logger.LogInformation(new EventId(3003, "BootstrapCompleted"),
                "API bootstrap completed; outcome={Outcome}", "Succeeded");
        }
        catch (Exception)
        {
            // Provider exceptions can contain connection details. Keep bootstrap logs value-free.
            app.Logger.LogError(new EventId(3004, "BootstrapFailed"),
                "Development bootstrap failed; outcome={Outcome}. Check database and bootstrap configuration.", "Failed");
            return 1;
        }
        await app.RunAsync();
        return 0;
    }

    public static WebApplication BuildApplication(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddJsonConsole();
        builder.Services.AddAlertsIdentity(builder.Configuration, builder.Environment.IsDevelopment());
        builder.Services.AddAlertRouting();
        builder.Services.AddHealthChecks();
        configure?.Invoke(builder);
        var app = builder.Build();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
            app.UseHsts();
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAuthEndpoints();
        app.MapHealthChecks("/health").AllowAnonymous();
        return app;
    }
}
