using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Alerts.Api.Infrastructure.Identity;

public sealed class AlertsIdentityDbContext(DbContextOptions<AlertsIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser>(options);
