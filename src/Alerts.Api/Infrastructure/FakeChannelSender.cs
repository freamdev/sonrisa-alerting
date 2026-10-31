using System.Collections.Concurrent;
using Alerts.Api.Application;

namespace Alerts.Api.Infrastructure;

// Recording adapter for isolated tests; never registered by the runtime.
public sealed class FakeChannelSender(string channelCode) : IChannelSender
{
    private readonly ConcurrentQueue<DeliveryMessage> calls = new();
    public string ChannelCode { get; } = channelCode;
    public IReadOnlyList<DeliveryMessage> Calls => calls.ToArray();

    public Task SendAsync(DeliveryMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Enqueue(message);
        return Task.CompletedTask;
    }
}
