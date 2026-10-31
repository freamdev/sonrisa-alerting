using Alerts.Api.Application;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Alerts.Api.ManagementEndpointSupport;

namespace Alerts.Api;

public static class AdministrationEndpoints
{
    public static void MapAdministrationEndpoints(this WebApplication app)
    {
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
}
