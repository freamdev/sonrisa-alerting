namespace Alerts.Api.Domain;

public sealed class Topic
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
}
