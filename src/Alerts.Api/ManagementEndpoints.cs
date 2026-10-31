using System.Security.Claims;
using Alerts.Api.Application;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Alerts.Api;

public static class ManagementEndpoints
{
    public static void MapManagementEndpoints(this WebApplication app)
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

        var admin = app.MapGroup("/api/admin").RequireAuthorization(AccessPolicies.Admin)
            .WithTags("Administration").AddEndpointFilter<NoStoreFilter>();
        admin.MapGet("/users", async (AlertsIdentityDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Users.AsNoTracking().OrderBy(x => x.Id)
                .Select(x => new UserItem(x.Id, x.Email, x.IsEnabled)).ToArrayAsync(ct)))
            .Produces<UserItem[]>().WithSummary("List user IDs, emails and enabled status; no credentials.");
        Mutation(admin.MapPut("/users/{id}/enabled", SetUserEnabledAsync), "Enable/disable a user; self-disable is rejected.");
        Mutation(admin.MapDelete("/users/{id}", DeleteUserAsync), "Hard-delete a user and their selections/deliveries; self-delete is rejected.");
        admin.MapGet("/topics", ReadTopicsAsync).Produces<CatalogItem[]>().WithSummary("List all global topics.");
        admin.MapGet("/channels", ReadChannelsAsync).Produces<CatalogItem[]>().WithSummary("List all global channels.");
        Mutation(admin.MapPut("/topics/{id}/enabled", (int id, EnabledRequest request, AlertsIdentityDbContext db, CancellationToken ct) =>
            SetCatalogEnabledAsync(id, request, db, true, ct)), "Set global topic enablement without rewriting preferences.");
        Mutation(admin.MapPut("/channels/{id}/enabled", (int id, EnabledRequest request, AlertsIdentityDbContext db, CancellationToken ct) =>
            SetCatalogEnabledAsync(id, request, db, false, ct)), "Set global channel enablement without rewriting preferences.");
    }

    private static void Mutation(RouteHandlerBuilder endpoint, string summary) => endpoint
        .AddEndpointFilter<ValidateAntiforgeryFilter>().WithSummary(summary)
        .WithDescription("Requires a session cookie and X-CSRF-TOKEN obtained after login from GET /api/auth/csrf. " +
            "400: invalid input/CSRF; 401: no active session; 403: insufficient role; 404: unknown ID; 409: disabled selection, self-action or concurrent user change.")
        .Produces(StatusCodes.Status204NoContent).Produces<ApiError>(400).Produces(401).Produces(403)
        .Produces<ApiError>(404).Produces<ApiError>(409);

    private static async Task<IResult> ReadTopicsAsync(AlertsIdentityDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Topics.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new CatalogItem(x.Id, x.Code, x.DisplayName, x.IsEnabled)).ToArrayAsync(ct));

    private static async Task<IResult> ReadChannelsAsync(AlertsIdentityDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Channels.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new CatalogItem(x.Id, x.Code, x.DisplayName, x.IsEnabled)).ToArrayAsync(ct));

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

    private static async Task<IResult> SetCatalogEnabledAsync(int id, EnabledRequest request,
        AlertsIdentityDbContext db, bool topic, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        if (request.IsEnabled is null) return InvalidEnabled();
        if (topic)
        {
            var item = await db.Topics.FindAsync([id], ct);
            if (item is null) return Missing();
            item.IsEnabled = request.IsEnabled.Value;
        }
        else
        {
            var item = await db.Channels.FindAsync([id], ct);
            if (item is null) return Missing();
            item.IsEnabled = request.IsEnabled.Value;
        }
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetUserEnabledAsync(string id, EnabledRequest request,
        HttpContext context, UserManager<ApplicationUser> users)
    {
        if (request.IsEnabled is null) return InvalidEnabled();
        if (!request.IsEnabled.Value && id == UserId(context)) return Conflict("self_disable_forbidden");
        var user = await users.FindByIdAsync(id);
        if (user is null) return Missing();
        user.IsEnabled = request.IsEnabled.Value;
        return (await users.UpdateAsync(user)).Succeeded ? Results.NoContent() : Conflict("user_change_conflict");
    }

    private static async Task<IResult> DeleteUserAsync(string id, HttpContext context, UserManager<ApplicationUser> users)
    {
        if (id == UserId(context)) return Conflict("self_delete_forbidden");
        var user = await users.FindByIdAsync(id);
        if (user is null) return Missing();
        // The approved EF/PostgreSQL cascade removes personal selections and deliveries,
        // while shared events and catalogs remain. Identity handles its own dependent data.
        return (await users.DeleteAsync(user)).Succeeded ? Results.NoContent() : Conflict("user_change_conflict");
    }

    private static string UserId(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated Identity session requires a user ID.");
    private static IResult InvalidId() => Results.BadRequest(new ApiError("invalid_id"));
    private static IResult InvalidEnabled() => Results.BadRequest(new ApiError("is_enabled_required"));
    private static IResult Missing() => Results.NotFound(new ApiError("not_found"));
    private static IResult Conflict(string error) => Results.Conflict(new ApiError(error));

    public sealed record CatalogItem(int Id, string Code, string DisplayName, bool IsEnabled);
    public sealed record UserItem(string Id, string? Email, bool IsEnabled);
    public sealed record EnabledRequest(bool? IsEnabled);
    public sealed record ApiError(string Error);
}

public sealed class NoStoreFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return next(context);
    }
}
