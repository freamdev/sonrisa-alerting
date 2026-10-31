using Alerts.Api.Domain;

namespace Alerts.Api.Application;

public interface INewsSource
{
    Task<IReadOnlyList<AlertEvent>> ReadAsync(CancellationToken cancellationToken);
}
