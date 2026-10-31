using System.Security.Claims;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Alerts.Api;

internal static class ManagementEndpointSupport
{
    internal static void Mutation(RouteHandlerBuilder endpoint, string summary) => endpoint
        .AddEndpointFilter<ValidateAntiforgeryFilter>().WithSummary(summary)
        .WithDescription("Requires a session cookie and X-CSRF-TOKEN obtained after login from GET /api/auth/csrf. " +
            "400: invalid input/CSRF; 401: no active session; 403: insufficient role; 404: unknown ID; 409: disabled selection, self-action or concurrent user change.")
        .Produces(StatusCodes.Status204NoContent).Produces<ApiError>(400).Produces(401).Produces(403)
        .Produces<ApiError>(404).Produces<ApiError>(409);

    internal static async Task<IResult> ReadTopicsAsync(AlertsIdentityDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Topics.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new CatalogItem(x.Id, x.Code, x.DisplayName, x.IsEnabled)).ToArrayAsync(ct));

    internal static async Task<IResult> ReadChannelsAsync(AlertsIdentityDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Channels.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new CatalogItem(x.Id, x.Code, x.DisplayName, x.IsEnabled)).ToArrayAsync(ct));

    internal static string UserId(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated Identity session requires a user ID.");
    internal static IResult InvalidId() => Results.BadRequest(new ApiError("invalid_id"));
    internal static IResult InvalidEnabled() => Results.BadRequest(new ApiError("is_enabled_required"));
    internal static IResult Missing() => Results.NotFound(new ApiError("not_found"));
    internal static IResult Conflict(string error) => Results.Conflict(new ApiError(error));
}

public sealed class NoStoreFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return next(context);
    }
}
