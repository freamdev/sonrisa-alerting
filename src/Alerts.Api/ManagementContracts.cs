namespace Alerts.Api;

public sealed record CatalogItem(int Id, string Code, string DisplayName, bool IsEnabled);
public sealed record UserItem(string Id, string? Email, bool IsEnabled);
public sealed record EnabledRequest(bool? IsEnabled);
public sealed record ApiError(string Error);
