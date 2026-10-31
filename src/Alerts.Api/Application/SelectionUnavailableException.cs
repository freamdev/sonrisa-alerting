namespace Alerts.Api.Application;

public sealed class SelectionUnavailableException : Exception
{
    public SelectionUnavailableException() : base("Disabled items cannot be newly selected.") { }
}
