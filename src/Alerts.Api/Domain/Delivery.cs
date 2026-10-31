namespace Alerts.Api.Domain;

public sealed class Delivery
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string UserId { get; set; } = "";
    public int ChannelId { get; set; }
    public DeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    // Store only a safe error code/summary, never raw exceptions or provider payloads.
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
