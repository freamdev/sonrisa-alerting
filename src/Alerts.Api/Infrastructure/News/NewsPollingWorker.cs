using Alerts.Api.Application;
using Microsoft.Extensions.Options;

namespace Alerts.Api.Infrastructure.News;

public sealed class NewsPollingWorker(IServiceScopeFactory scopes, IOptions<NewsSourceOptions> options,
    TimeProvider clock, ILogger<NewsPollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var stage = "source";
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var source = scope.ServiceProvider.GetRequiredService<INewsSource>();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IAlertDispatcher>();
                    var events = await source.ReadAsync(stoppingToken);
                    stage = "dispatch";
                    var added = 0;
                    foreach (var alert in events)
                    {
                        if (await dispatcher.ProcessAsync(alert, stoppingToken)) added++;
                    }
                    logger.LogInformation("News poll completed: {AcceptedCount} valid items, {NewCount} new events",
                        events.Count, added);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // URLs, response bodies and exception messages may contain secrets.
                    logger.LogWarning("News polling cycle failed during {Stage}; will retry next interval", stage);
                }
                // Delay after completion, so HTTP requests and dispatch never overlap cycles.
                await Task.Delay(TimeSpan.FromSeconds(options.Value.IntervalSeconds), clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { logger.LogInformation("News polling stopped"); }
    }
}
