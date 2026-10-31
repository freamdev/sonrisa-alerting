namespace Alerts.Api.Application;

// Only fixed, application-owned codes may cross the provider boundary.
public sealed class ChannelDeliveryException : Exception
{
    public string ErrorCode { get; }

    public ChannelDeliveryException(ChannelDeliveryError error) : base(error.ToString()) =>
        ErrorCode = error switch
        {
            ChannelDeliveryError.MissingDestination => "destination_missing",
            ChannelDeliveryError.InvalidDestination => "destination_invalid",
            ChannelDeliveryError.InvalidConfiguration => "configuration_invalid",
            ChannelDeliveryError.ProviderRejected => "provider_rejected",
            _ => "provider_unavailable"
        };
}
