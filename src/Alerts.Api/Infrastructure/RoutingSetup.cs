using Alerts.Api.Application;

namespace Alerts.Api.Infrastructure;

public static class RoutingSetup
{
    public static IServiceCollection AddAlertRouting(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DispatchGate>();
        services.AddSingleton<IChannelSender>(new FakeChannelSender("email"));
        services.AddSingleton<IChannelSender>(new FakeChannelSender("slack"));
        services.AddScoped<IAlertDispatcher, AlertDispatcher>();
        services.AddHostedService<PendingDeliveryRecovery>();
        return services;
    }
}
