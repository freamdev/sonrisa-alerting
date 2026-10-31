using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Alerts.Api.Infrastructure.Identity;

public sealed class EnabledCookieEvents(
    ISecurityStampValidator stampValidator,
    UserManager<ApplicationUser> users) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        // Preserve Identity's password/security-stamp and role refresh behavior.
        await stampValidator.ValidateAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var user = await users.GetUserAsync(context.Principal);
        if (user is not { IsEnabled: true })
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
