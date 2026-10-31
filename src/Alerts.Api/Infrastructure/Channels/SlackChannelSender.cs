using System.Net.Http.Json;
using System.Text.Json;
using Alerts.Api.Application;
using Microsoft.Extensions.Options;

namespace Alerts.Api.Infrastructure.Channels;

public sealed class SlackChannelSender(HttpClient client, IOptions<SlackOptions> options,
    IHostEnvironment environment) : IChannelSender
{
    public string ChannelCode => "slack";

    public async Task SendAsync(DeliveryMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, string>? destinations;
        try
        {
            var json = options.Value.WebhooksJson;
            destinations = JsonSerializer.Deserialize<Dictionary<string, string>>(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
        catch (JsonException) { throw new ChannelDeliveryException(ChannelDeliveryError.InvalidConfiguration); }
        if (destinations is null)
            throw new ChannelDeliveryException(ChannelDeliveryError.InvalidConfiguration);
        if (!destinations.TryGetValue(message.UserId, out var destination) || string.IsNullOrWhiteSpace(destination))
            throw new ChannelDeliveryException(ChannelDeliveryError.MissingDestination);
        if (!Uri.TryCreate(destination, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !IsAllowed(uri))
            throw new ChannelDeliveryException(ChannelDeliveryError.InvalidDestination);

        // One plain-text block (Slack limit 3000 UTF-16 units). Disable fallback parsing and unfurls.
        var text = AlertMessageFormatter.Body(message);
        if (text.Length > 3000)
        {
            var length = char.IsHighSurrogate(text[2998]) ? 2998 : 2999;
            text = text[..length] + "…";
        }
        try
        {
            using var response = await client.PostAsJsonAsync(uri, new
            {
                text = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"),
                mrkdwn = false,
                parse = "none",
                unfurl_links = false,
                unfurl_media = false,
                blocks = new[] { new { type = "section", text = new { type = "plain_text", text, emoji = false } } }
            }, cancellationToken);
            // Never read, log or persist provider response bodies (may include sensitive data).
            if (!response.IsSuccessStatusCode)
                throw new ChannelDeliveryException(ChannelDeliveryError.ProviderRejected);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ChannelDeliveryException) { throw; }
        catch (Exception) { throw new ChannelDeliveryException(ChannelDeliveryError.ProviderUnavailable); }
    }

    private bool IsAllowed(Uri uri) =>
        (uri.Scheme == "https" && uri.IsDefaultPort && uri.Host == "hooks.slack.com" &&
            uri.AbsolutePath.StartsWith("/services/", StringComparison.Ordinal)) ||
        (environment.IsDevelopment() && options.Value.AllowLocalMock && uri.Scheme == "http" &&
            (uri.IsLoopback || uri.Host == "slack-mock"));
}
