using Alerts.Api.Application;
using Alerts.Api.Domain;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Alerts.Api.Infrastructure;

public sealed class AlertDispatcher(AlertsIdentityDbContext db, IEnumerable<IChannelSender> senders,
    DispatchGate gate, TimeProvider clock, ILogger<AlertDispatcher> logger) : IAlertDispatcher
{
    public async Task<bool> ProcessAsync(AlertEvent alert, CancellationToken cancellationToken = default)
    {
        Validate(alert);
        await gate.Semaphore.WaitAsync(cancellationToken);
        try
        {
            if (await db.AlertEvents.AnyAsync(x => x.ExternalEventId == alert.ExternalEventId, cancellationToken))
                return false;
            if (!await db.Topics.AnyAsync(x => x.Id == alert.TopicId, cancellationToken))
                throw new ArgumentException("Unknown topic.", nameof(alert));

            var stored = new AlertEvent
            {
                ExternalEventId = alert.ExternalEventId,
                TopicId = alert.TopicId,
                Title = alert.Title,
                Body = alert.Body,
                OccurredAt = alert.OccurredAt,
                ReceivedAt = clock.GetUtcNow()
            };
            // One transaction commits the event and its entire initial routing snapshot.
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            List<Delivery> deliveries = [];
            try
            {
                db.AlertEvents.Add(stored);
                await db.SaveChangesAsync(cancellationToken);
                var recipients = await (from topic in db.Topics
                                        join selection in db.UserTopicSelections on topic.Id equals selection.TopicId
                                        join user in db.Users on selection.UserId equals user.Id
                                        join preference in db.UserChannelSelections on user.Id equals preference.UserId
                                        join channel in db.Channels on preference.ChannelId equals channel.Id
                                        where topic.Id == stored.TopicId && topic.IsEnabled && user.IsEnabled && channel.IsEnabled
                                        orderby user.Id, channel.Id
                                        select new { UserId = user.Id, ChannelId = channel.Id }).ToListAsync(cancellationToken);
                deliveries = recipients.Select(x => new Delivery
                {
                    EventId = stored.Id,
                    UserId = x.UserId,
                    ChannelId = x.ChannelId,
                    CreatedAt = clock.GetUtcNow()
                }).ToList();
                db.Deliveries.AddRange(deliveries);
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsDuplicateEvent(ex))
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(cancellationToken);
                db.Entry(stored).State = EntityState.Detached;
                foreach (var delivery in deliveries)
                    db.Entry(delivery).State = EntityState.Detached;
                return false;
            }
            catch
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(CancellationToken.None);
                db.Entry(stored).State = EntityState.Detached;
                foreach (var delivery in deliveries)
                    db.Entry(delivery).State = EntityState.Detached;
                throw;
            }
            foreach (var delivery in deliveries)
                await SendPendingAsync(delivery, cancellationToken);
            return true;
        }
        finally { gate.Semaphore.Release(); }
    }

    public async Task RetryPendingAsync(CancellationToken cancellationToken = default)
    {
        await gate.Semaphore.WaitAsync(cancellationToken);
        try
        {
            // Query by ID in bounded pages; do not keep an unbounded pending queue in memory.
            long after = 0;
            while (true)
            {
                var page = await db.Deliveries.Where(x => x.Status == DeliveryStatus.Pending && x.Id > after)
                    .OrderBy(x => x.Id).Take(100).ToListAsync(cancellationToken);
                if (page.Count == 0) break;
                foreach (var delivery in page)
                {
                    await db.Entry(delivery).ReloadAsync(cancellationToken);
                    await SendPendingAsync(delivery, cancellationToken);
                    after = delivery.Id;
                }
            }
        }
        finally { gate.Semaphore.Release(); }
    }

    private async Task SendPendingAsync(Delivery delivery, CancellationToken ct)
    {
        if (delivery.Status != DeliveryStatus.Pending) return;
        var alert = await db.AlertEvents.AsNoTracking().SingleAsync(x => x.Id == delivery.EventId, ct);
        while (delivery.AttemptCount < 3)
        {
            ct.ThrowIfCancellationRequested();
            if (delivery.LastAttemptAt is { } last)
            {
                var due = last.AddSeconds(Math.Pow(2, delivery.AttemptCount - 1));
                var remaining = due - clock.GetUtcNow();
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, clock, ct);
            }
            // Scalar/no-tracking DB queries deliberately avoid cached enablement values.
            var channel = await db.Channels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == delivery.ChannelId && x.IsEnabled, ct);
            if (channel is null ||
                !await db.Users.AnyAsync(x => x.Id == delivery.UserId && x.IsEnabled, ct) ||
                !await db.Topics.AnyAsync(x => x.Id == alert.TopicId && x.IsEnabled, ct) ||
                !await db.UserTopicSelections.AnyAsync(x => x.UserId == delivery.UserId && x.TopicId == alert.TopicId, ct) ||
                !await db.UserChannelSelections.AnyAsync(x => x.UserId == delivery.UserId && x.ChannelId == delivery.ChannelId, ct))
                return; // Preserve pending state without consuming a send attempt.

            var sender = senders.SingleOrDefault(x => x.ChannelCode == channel.Code);
            if (sender is null)
            {
                delivery.LastError = "sender_unavailable";
                await db.SaveChangesAsync(ct);
                return;
            }
            delivery.AttemptCount++;
            delivery.LastAttemptAt = clock.GetUtcNow();
            // Reserve before I/O: interruption can consume an attempt but never resets the cap.
            await db.SaveChangesAsync(ct);
            try
            {
                await sender.SendAsync(new DeliveryMessage(delivery.Id, delivery.UserId, alert.ExternalEventId,
                    alert.TopicId, alert.Title, alert.Body, alert.OccurredAt), ct);
                delivery.Status = DeliveryStatus.Succeeded;
                delivery.LastError = null;
                delivery.CompletedAt = clock.GetUtcNow();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                // Never persist or log raw provider exceptions, payloads or destinations.
                delivery.LastError = "send_failed";
                if (delivery.AttemptCount == 3)
                {
                    delivery.Status = DeliveryStatus.Failed;
                    delivery.CompletedAt = clock.GetUtcNow();
                }
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Delivery {DeliveryId} event {EventId} channel {ChannelId}: {Status}, attempt {AttemptCount}",
                delivery.Id, delivery.EventId, delivery.ChannelId, delivery.Status, delivery.AttemptCount);
            if (delivery.Status != DeliveryStatus.Pending) return;
        }
        // Recover an interruption during the final reserved attempt without a fourth call.
        delivery.Status = DeliveryStatus.Failed;
        delivery.LastError ??= "attempt_limit_reached";
        delivery.CompletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Delivery {DeliveryId} event {EventId}: final failure after {AttemptCount} reserved attempts",
            delivery.Id, delivery.EventId, delivery.AttemptCount);
    }

    internal static bool IsDuplicateEvent(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_AlertEvents_ExternalEventId"
        };

    private static void Validate(AlertEvent alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        if (string.IsNullOrWhiteSpace(alert.ExternalEventId) || alert.ExternalEventId.Length > 256 ||
            string.IsNullOrWhiteSpace(alert.Title) || alert.Title.Length > 500 || alert.Body is null ||
            alert.TopicId <= 0 || alert.OccurredAt == default || alert.OccurredAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("An event requires a bounded external ID/title, body, topic and UTC occurrence timestamp.", nameof(alert));
    }
}
