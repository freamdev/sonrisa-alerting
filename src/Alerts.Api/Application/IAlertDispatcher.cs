using Alerts.Api.Domain;

namespace Alerts.Api.Application;

public interface IAlertDispatcher
{
    // Returns false for a previously recorded external ID. Input is copied, never tracked.
    Task<bool> ProcessAsync(AlertEvent alert, CancellationToken cancellationToken = default);
    Task RetryPendingAsync(CancellationToken cancellationToken = default);
}
