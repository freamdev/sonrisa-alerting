namespace Alerts.Api.Application;

public enum ChannelDeliveryError
{
    MissingDestination,
    InvalidDestination,
    InvalidConfiguration,
    ProviderRejected,
    ProviderUnavailable
}
