using System.Globalization;
using Alerts.Api.Application;

namespace Alerts.Api.Infrastructure.Channels;

public static class AlertMessageFormatter
{
    public static string Subject(DeliveryMessage message) =>
        "[Alerts] " + new string(message.Title.Where(c => !char.IsControl(c)).ToArray());

    // Plain text MIME and Slack plain_text blocks avoid HTML/Slack markup interpretation.
    public static string Body(DeliveryMessage message) =>
        $"{message.Title}\n\n{message.Body}\n\nOccurred at: {message.OccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}";
}
