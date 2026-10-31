using Alerts.Api.Application;
using static Alerts.Api.ManagementEndpointSupport;

namespace Alerts.Api;

public static class PersonalSettingsEndpoints
{
    public static void MapPersonalSettingsEndpoints(this WebApplication app)
    {
        var personal = app.MapGroup("/api/me").RequireAuthorization(AccessPolicies.User)
            .WithTags("Personal settings").AddEndpointFilter<NoStoreFilter>();
        personal.MapGet("/topics", ReadTopicsAsync).Produces<CatalogItem[]>()
            .WithSummary("List topics, including globally disabled items.");
        personal.MapGet("/channels", ReadChannelsAsync).Produces<CatalogItem[]>()
            .WithSummary("List channels, including globally disabled items.");
        personal.MapGet("/selections", async (HttpContext context, ISelectionService selections, CancellationToken ct) =>
            Results.Ok(await selections.ReadAsync(UserId(context), ct)))
            .Produces<UserSelections>().WithSummary("Read independent topicIds and channelIds for this session.");
        Mutation(personal.MapPut("/topics/{id}", (int id, HttpContext context, ISelectionService selections, CancellationToken ct) =>
            SetSelectionAsync(id, context, selections, true, true, ct)), "Select an enabled topic for this session.");
        Mutation(personal.MapDelete("/topics/{id}", (int id, HttpContext context, ISelectionService selections, CancellationToken ct) =>
            SetSelectionAsync(id, context, selections, true, false, ct)), "Remove a topic selection for this session.");
        Mutation(personal.MapPut("/channels/{id}", (int id, HttpContext context, ISelectionService selections, CancellationToken ct) =>
            SetSelectionAsync(id, context, selections, false, true, ct)), "Select an enabled channel for all selected topics.");
        Mutation(personal.MapDelete("/channels/{id}", (int id, HttpContext context, ISelectionService selections, CancellationToken ct) =>
            SetSelectionAsync(id, context, selections, false, false, ct)), "Remove a channel selection for all selected topics.");
    }

    private static async Task<IResult> SetSelectionAsync(int id, HttpContext context, ISelectionService selections,
        bool topic, bool selected, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        try
        {
            if (topic) await selections.SetTopicAsync(UserId(context), id, selected, ct);
            else await selections.SetChannelAsync(UserId(context), id, selected, ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Missing(); }
        catch (SelectionUnavailableException) { return Conflict("item_disabled"); }
    }
}
