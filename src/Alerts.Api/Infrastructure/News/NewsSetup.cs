using Alerts.Api.Application;

namespace Alerts.Api.Infrastructure.News;

public static class NewsSetup
{
    public static IServiceCollection AddNewsPolling(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NewsSourceOptions>().Bind(configuration.GetSection("NewsSource"))
            .Validate(x => !x.Enabled || Uri.TryCreate(x.Endpoint, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"), "NewsSource requires an absolute HTTP(S) endpoint.")
            .Validate(x => x.IntervalSeconds is >= 1 and <= 86400, "NewsSource interval must be 1..86400 seconds.")
            .Validate(x => x.TimeoutSeconds is >= 1 and <= 120, "NewsSource timeout must be 1..120 seconds.")
            .ValidateOnStart();
        services.AddHttpClient<INewsSource, HttpNewsSource>((provider, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NewsSourceOptions>>().Value.TimeoutSeconds);
            client.MaxResponseContentBufferSize = 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllLoggers();
        services.AddHostedService<NewsPollingWorker>();
        return services;
    }

    public static void MapDevelopmentNews(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        app.MapGet("/dev/mock-news", async (Microsoft.Extensions.Options.IOptions<NewsSourceOptions> options,
            CancellationToken ct) =>
        {
            try
            {
                var path = Path.GetFullPath(options.Value.FixturePath, app.Environment.ContentRootPath);
                var json = await File.ReadAllTextAsync(path, ct);
                return Results.Content(json, "application/json");
            }
            catch (IOException) { return Results.StatusCode(503); }
            catch (UnauthorizedAccessException) { return Results.StatusCode(503); }
        }).AllowAnonymous();
    }
}
