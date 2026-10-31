namespace Alerts.Api.Domain;

public sealed class AlertEvent
{
    public long Id { get; set; }
    public string ExternalEventId { get; set; } = "";
    public int TopicId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
