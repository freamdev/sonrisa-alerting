using Alerts.Api.Infrastructure.Identity;

namespace Alerts.Api;

public static class Program
{
    public static async Task Main(string[] args)
    {
        await using var app = BuildApplication(args);
        await DevelopmentAccountSeeder.SeedAsync(app.Services, app.Configuration, app.Environment);
        await app.RunAsync();
    }

    public static WebApplication BuildApplication(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddAlertsIdentity(builder.Configuration, builder.Environment.IsDevelopment());
        configure?.Invoke(builder);
        var app = builder.Build();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
            app.UseHsts();
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAuthEndpoints();
        return app;
    }
}
