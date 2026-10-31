namespace Alerts.Api.Infrastructure.Channels;

public sealed class SlackOptions
{
    // JSON object: application user ID -> incoming webhook URL. Treat as a secret.
    public string WebhooksJson { get; set; } = "{}";
    public bool AllowLocalMock { get; set; }
}
