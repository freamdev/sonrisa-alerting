using Alerts.Api.Application;
using Alerts.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace Alerts.Api;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        auth.MapPost("/login", async (LoginRequest request, SignInManager<ApplicationUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return Results.Unauthorized();
            }
            var result = await signIn.PasswordSignInAsync(request.Email, request.Password,
                isPersistent: false, lockoutOnFailure: true);
            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
        }).AddEndpointFilter<ValidateAntiforgeryFilter>();

        auth.MapPost("/logout", async (SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization(AccessPolicies.User).AddEndpointFilter<ValidateAntiforgeryFilter>();

        // This is also the session check. Admins retain the ordinary user capability.
        auth.MapGet("/me", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new
            {
                id = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                email = context.User.Identity?.Name,
                isAdmin = context.User.IsInRole(AccessPolicies.AdminRole)
            });
        }).RequireAuthorization(AccessPolicies.User);

        app.MapGet("/api/admin/probe", () => Results.Ok(new { authorized = true }))
            .RequireAuthorization(AccessPolicies.Admin);
    }

    public sealed record LoginRequest(string? Email, string? Password);
}

public sealed class ValidateAntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new { error = "Invalid CSRF token." });
        }
        return await next(context);
    }
}
