using Alerts.Api.Application;

namespace Alerts.Api.Infrastructure;

// A single startup recovery pass, not a polling/source ingestion worker.
public sealed class PendingDeliveryRecovery(IServiceScopeFactory scopes, ILogger<PendingDeliveryRecovery> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IAlertDispatcher>().RetryPendingAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception)
        {
            logger.LogError("Pending delivery recovery failed; persisted pending rows remain available for a later scan.");
        }
    }
}
