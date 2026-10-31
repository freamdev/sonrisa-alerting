using System.Globalization;
using System.Text.Json;
using Alerts.Api.Application;
using Alerts.Api.Domain;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Alerts.Api.Infrastructure.News;

public sealed class HttpNewsSource(HttpClient client, AlertsIdentityDbContext db,
    IOptions<NewsSourceOptions> options, ILogger<HttpNewsSource> logger) : INewsSource
{
    public async Task<IReadOnlyList<AlertEvent>> ReadAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(options.Value.Endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected a news array.");
        var topics = await db.Topics.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
        List<AlertEvent> events = [];
        var index = 0;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var alert = Normalize(item, topics);
            if (alert is null)
                logger.LogWarning("Rejected malformed or unknown-topic news item at index {ItemIndex}", index);
            else
                events.Add(alert);
            index++;
        }
        return events;
    }

    private static AlertEvent? Normalize(JsonElement item, Dictionary<string, int> topics)
    {
        static string? Field(JsonElement value, string name) =>
            value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) &&
            field.ValueKind == JsonValueKind.String ? field.GetString() : null;
        var id = Field(item, "id");
        var topic = Field(item, "topic");
        var title = Field(item, "title");
        var body = Field(item, "body");
        var timestamp = Field(item, "timestamp");
        // Require an explicit zone; never infer the execution host's local timezone.
        var hasZone = timestamp is not null && (timestamp.EndsWith('Z') ||
            timestamp.Length >= 6 && timestamp[^3] == ':' && timestamp[^6] is '+' or '-');
        if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || string.IsNullOrWhiteSpace(title) ||
            title.Length > 500 || body is null || topic is null || !topics.TryGetValue(topic, out var topicId) ||
            !hasZone || !DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurredAt) ||
            occurredAt.ToUniversalTime() == default)
            return null;
        return new AlertEvent
        {
            ExternalEventId = id,
            TopicId = topicId,
            Title = title,
            Body = body,
            OccurredAt = occurredAt.ToUniversalTime()
        };
    }
}
