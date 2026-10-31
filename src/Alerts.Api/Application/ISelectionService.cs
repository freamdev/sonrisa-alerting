namespace Alerts.Api.Application;

public sealed record UserSelections(IReadOnlyList<int> TopicIds, IReadOnlyList<int> ChannelIds);

// Internal use cases. HTTP callers must derive userId from the authenticated principal.
// Global enablement does not rewrite preferences; delivery eligibility is a later concern.
public interface ISelectionService
{
    Task<UserSelections> ReadAsync(string userId, CancellationToken cancellationToken = default);
    Task SetTopicAsync(string userId, int topicId, bool selected, CancellationToken cancellationToken = default);
    Task SetChannelAsync(string userId, int channelId, bool selected, CancellationToken cancellationToken = default);
}
