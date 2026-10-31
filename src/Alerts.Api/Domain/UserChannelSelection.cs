namespace Alerts.Api.Domain;

public sealed class UserChannelSelection
{
    public string UserId { get; set; } = "";
    public int ChannelId { get; set; }
}
