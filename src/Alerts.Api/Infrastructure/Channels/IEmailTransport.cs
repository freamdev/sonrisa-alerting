using MimeKit;

namespace Alerts.Api.Infrastructure.Channels;

public interface IEmailTransport
{
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);
}
