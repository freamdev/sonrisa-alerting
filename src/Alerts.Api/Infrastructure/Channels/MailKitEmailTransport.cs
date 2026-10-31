using Alerts.Api.Application;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Alerts.Api.Infrastructure.Channels;

public sealed class MailKitEmailTransport(IOptions<EmailOptions> options, IHostEnvironment environment) : IEmailTransport
{
    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var security = settings.Security switch
        {
            "StartTls" => SecureSocketOptions.StartTls,
            "SslOnConnect" => SecureSocketOptions.SslOnConnect,
            "None" when environment.IsDevelopment() => SecureSocketOptions.None,
            _ => throw new ChannelDeliveryException(ChannelDeliveryError.InvalidConfiguration)
        };
        if (string.IsNullOrWhiteSpace(settings.Host) || settings.Port is < 1 or > 65535 ||
            (string.IsNullOrEmpty(settings.Username) != string.IsNullOrEmpty(settings.Password)))
            throw new ChannelDeliveryException(ChannelDeliveryError.InvalidConfiguration);

        using var client = new SmtpClient { Timeout = 30000 };
        // No protocol logger: SMTP transcripts can contain credentials and message contents.
        await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);
        if (!string.IsNullOrEmpty(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        // Acceptance is success even if QUIT fails; do not resend an accepted message for that.
        try { await client.DisconnectAsync(true, cancellationToken); }
        catch (Exception) { }
    }
}
