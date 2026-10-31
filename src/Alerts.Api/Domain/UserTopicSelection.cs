namespace Alerts.Api.Domain;

public sealed class UserTopicSelection
{
    public string UserId { get; set; } = "";
    public int TopicId { get; set; }
}
