using Alerts.Api.Domain;
using Alerts.Api.Infrastructure;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Alerts.UnitTests;

// InMemory verifies use cases only; PostgreSQL validation separately verifies constraints.
public sealed class SelectionTests
{
    [Fact]
    public async Task SelectionsAreIndependentRepeatablePersistedAndIsolatedByUser()
    {
        var options = Options();
        await using (var db = new AlertsIdentityDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(new ApplicationUser { Id = "alice" }, new ApplicationUser { Id = "bob" });
            await db.SaveChangesAsync();
            var service = new SelectionService(db);
            Assert.Empty((await service.ReadAsync("alice")).TopicIds);
            Assert.Empty((await service.ReadAsync("alice")).ChannelIds);
            await service.SetTopicAsync("alice", 1, true);
            await service.SetTopicAsync("alice", 3, true);
            await service.SetTopicAsync("alice", 1, true);
            Assert.Empty((await service.ReadAsync("alice")).ChannelIds);
            await service.SetChannelAsync("alice", 1, true);
            await service.SetChannelAsync("alice", 2, true);
            await service.SetChannelAsync("alice", 2, true);
            await service.SetTopicAsync("bob", 2, true);
        }
        await using (var db = new AlertsIdentityDbContext(options))
        {
            var service = new SelectionService(db);
            var selections = await service.ReadAsync("alice");
            Assert.Equal(new[] { 1, 3 }, selections.TopicIds);
            Assert.Equal(new[] { 1, 2 }, selections.ChannelIds);
            await service.SetChannelAsync("alice", 2, false);
            await service.SetChannelAsync("alice", 2, false);
            Assert.Equal(new[] { 1, 3 }, (await service.ReadAsync("alice")).TopicIds);
            await service.SetTopicAsync("alice", 1, false);
            await service.SetTopicAsync("alice", 3, false);
            Assert.Empty((await service.ReadAsync("alice")).TopicIds);
            Assert.Equal(new[] { 1 }, (await service.ReadAsync("alice")).ChannelIds);
            Assert.Equal(new[] { 2 }, (await service.ReadAsync("bob")).TopicIds);
            Assert.Empty((await service.ReadAsync("bob")).ChannelIds);
        }
    }

    [Fact]
    public async Task UnknownReferencesAreRejectedWithoutPartialUpdates()
    {
        await using var db = new AlertsIdentityDbContext(Options());
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new ApplicationUser { Id = "alice" });
        await db.SaveChangesAsync();
        var service = new SelectionService(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReadAsync("missing"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetTopicAsync("missing", 1, true));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetChannelAsync("missing", 1, true));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetTopicAsync("alice", 999, true));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetChannelAsync("alice", 999, true));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReadAsync(" "));
        Assert.Empty(db.UserTopicSelections);
        Assert.Empty(db.UserChannelSelections);
    }

    [Fact]
    public async Task GlobalEnablementDoesNotRewriteOrAutomaticallyCreatePreferences()
    {
        await using var db = new AlertsIdentityDbContext(Options());
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new ApplicationUser { Id = "alice" });
        await db.SaveChangesAsync();
        var service = new SelectionService(db);
        await service.SetTopicAsync("alice", 1, true);
        await service.SetChannelAsync("alice", 2, true);
        (await db.Topics.FindAsync(1))!.IsEnabled = false;
        (await db.Channels.FindAsync(2))!.IsEnabled = false;
        await db.SaveChangesAsync();
        Assert.Equal(new[] { 1 }, (await service.ReadAsync("alice")).TopicIds);
        Assert.Equal(new[] { 2 }, (await service.ReadAsync("alice")).ChannelIds);
        (await db.Topics.FindAsync(1))!.IsEnabled = true;
        (await db.Channels.FindAsync(2))!.IsEnabled = true;
        await db.SaveChangesAsync();
        Assert.Equal(new[] { 1 }, (await service.ReadAsync("alice")).TopicIds);
        Assert.Equal(new[] { 2 }, (await service.ReadAsync("alice")).ChannelIds);
    }

    [Fact]
    public async Task CatalogSeedIsRepeatableAndPreservesChanges()
    {
        await using var db = new AlertsIdentityDbContext(Options());
        await db.Database.EnsureCreatedAsync();
        (await db.Topics.FindAsync(1))!.IsEnabled = false;
        await db.SaveChangesAsync();
        await db.Database.EnsureCreatedAsync();
        Assert.Equal(new[] { "finance", "politics", "weather" }, await db.Topics.OrderBy(x => x.Id).Select(x => x.Code).ToArrayAsync());
        Assert.Equal(new[] { "email", "slack" }, await db.Channels.OrderBy(x => x.Id).Select(x => x.Code).ToArrayAsync());
        Assert.False((await db.Topics.FindAsync(1))!.IsEnabled);
        Assert.Empty(db.UserTopicSelections);
        Assert.Empty(db.UserChannelSelections);
    }

    [Fact]
    public void NewDeliveryStartsPendingWithoutAttemptsOrSensitiveErrorData()
    {
        var delivery = new Delivery();
        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(0, delivery.AttemptCount);
        Assert.Null(delivery.LastError);
        Assert.Null(delivery.LastAttemptAt);
        Assert.Null(delivery.CompletedAt);
    }

    private static DbContextOptions<AlertsIdentityDbContext> Options() =>
        new DbContextOptionsBuilder<AlertsIdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
}
