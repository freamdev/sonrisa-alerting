using Alerts.Api.Application;
using Alerts.Api.Infrastructure.Channels;

namespace Alerts.Api.Infrastructure;

public static class RoutingSetup
{
    public static IServiceCollection AddAlertRouting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DispatchGate>();
        services.Configure<EmailOptions>(configuration.GetSection("Channels:Email"));
        services.Configure<SlackOptions>(configuration.GetSection("Channels:Slack"));
        services.AddScoped<IEmailTransport, MailKitEmailTransport>();
        services.AddScoped<IChannelSender, EmailChannelSender>();
        services.AddHttpClient<SlackChannelSender>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllLoggers(); // Default HttpClient loggers include the secret webhook URL.
        services.AddScoped<IChannelSender>(provider => provider.GetRequiredService<SlackChannelSender>());
        services.AddScoped<IAlertDispatcher, AlertDispatcher>();
        services.AddHostedService<PendingDeliveryRecovery>();
        return services;
    }
}
