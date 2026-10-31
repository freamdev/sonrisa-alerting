using Alerts.Api.Application;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Alerts.Api.Infrastructure.Channels;

public sealed class EmailChannelSender(AlertsIdentityDbContext db, IOptions<EmailOptions> options,
    IEmailTransport transport) : IChannelSender
{
    public string ChannelCode => "email";

    public async Task SendAsync(DeliveryMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var address = await db.Users.AsNoTracking().Where(x => x.Id == message.UserId)
            .Select(x => x.Email).SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(address))
            throw new ChannelDeliveryException(ChannelDeliveryError.MissingDestination);
        if (!IsMailbox(address, out var recipient))
            throw new ChannelDeliveryException(ChannelDeliveryError.InvalidDestination);
        if (!IsMailbox(options.Value.From, out var from))
            throw new ChannelDeliveryException(ChannelDeliveryError.InvalidConfiguration);

        using var mail = new MimeMessage();
        mail.From.Add(from);
        mail.To.Add(recipient);
        mail.Subject = AlertMessageFormatter.Subject(message);
        mail.Body = new TextPart("plain") { Text = AlertMessageFormatter.Body(message) };
        try { await transport.SendAsync(mail, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ChannelDeliveryException) { throw; }
        catch (MailKit.Net.Smtp.SmtpCommandException)
        { throw new ChannelDeliveryException(ChannelDeliveryError.ProviderRejected); }
        catch (Exception) { throw new ChannelDeliveryException(ChannelDeliveryError.ProviderUnavailable); }
    }

    private static bool IsMailbox(string? address, out MailboxAddress mailbox)
    {
        mailbox = null!;
        if (string.IsNullOrWhiteSpace(address) || address.Any(char.IsControl) ||
            !MailboxAddress.TryParse(address, out var parsed) || !parsed.Address.Contains('@'))
            return false;
        mailbox = parsed;
        return true;
    }
}
