namespace Alerts.Api.Application;

public sealed record DeliveryMessage(long DeliveryId, string UserId, string ExternalEventId,
    int TopicId, string Title, string Body, DateTimeOffset OccurredAt);

public interface IChannelSender
{
    string ChannelCode { get; }
    Task SendAsync(DeliveryMessage message, CancellationToken cancellationToken);
}
