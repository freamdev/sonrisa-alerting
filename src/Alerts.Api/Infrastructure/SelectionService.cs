using Alerts.Api.Application;
using Alerts.Api.Domain;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Alerts.Api.Infrastructure;

public sealed class SelectionService(AlertsIdentityDbContext db) : ISelectionService
{
    public async Task<UserSelections> ReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(userId, cancellationToken);
        return new UserSelections(
            await db.UserTopicSelections.Where(x => x.UserId == userId).OrderBy(x => x.TopicId)
                .Select(x => x.TopicId).ToListAsync(cancellationToken),
            await db.UserChannelSelections.Where(x => x.UserId == userId).OrderBy(x => x.ChannelId)
                .Select(x => x.ChannelId).ToListAsync(cancellationToken));
    }

    public async Task SetTopicAsync(string userId, int topicId, bool selected, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(userId, cancellationToken);
        if (!await db.Topics.AnyAsync(x => x.Id == topicId, cancellationToken))
            throw new KeyNotFoundException("Topic does not exist.");
        var selection = await db.UserTopicSelections.FindAsync([userId, topicId], cancellationToken);
        if (selected && selection is null)
            db.UserTopicSelections.Add(new UserTopicSelection { UserId = userId, TopicId = topicId });
        else if (!selected && selection is not null)
            db.UserTopicSelections.Remove(selection);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetChannelAsync(string userId, int channelId, bool selected, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(userId, cancellationToken);
        if (!await db.Channels.AnyAsync(x => x.Id == channelId, cancellationToken))
            throw new KeyNotFoundException("Channel does not exist.");
        var selection = await db.UserChannelSelections.FindAsync([userId, channelId], cancellationToken);
        if (selected && selection is null)
            db.UserChannelSelections.Add(new UserChannelSelection { UserId = userId, ChannelId = channelId });
        else if (!selected && selection is not null)
            db.UserChannelSelections.Remove(selection);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireUserAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (!await db.Users.AnyAsync(x => x.Id == userId, cancellationToken))
            throw new KeyNotFoundException("User does not exist.");
    }
}
