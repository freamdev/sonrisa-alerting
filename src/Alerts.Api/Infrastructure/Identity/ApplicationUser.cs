using Microsoft.AspNetCore.Identity;

namespace Alerts.Api.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public bool IsEnabled { get; set; } = true;
}
