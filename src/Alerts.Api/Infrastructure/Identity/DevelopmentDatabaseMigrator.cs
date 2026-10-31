using Microsoft.EntityFrameworkCore;

namespace Alerts.Api.Infrastructure.Identity;

public static class DevelopmentDatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services, IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Development migrations are allowed only in Development.");
        }

        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DevelopmentDatabaseMigrator));
        logger.LogInformation(new EventId(3001, "MigrationStarted"),
            "Development database migration started; outcome={Outcome}", "Started");
        var database = scope.ServiceProvider.GetRequiredService<AlertsIdentityDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
        logger.LogInformation(new EventId(3002, "MigrationCompleted"),
            "Development database migration completed; outcome={Outcome}", "Succeeded");
    }
}
