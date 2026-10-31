namespace Alerts.Api.Infrastructure.News;

public sealed class NewsSourceOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "http://localhost:5165/dev/mock-news";
    public int IntervalSeconds { get; set; } = 30;
    public int TimeoutSeconds { get; set; } = 10;
    public string FixturePath { get; set; } = "Fixtures/news.json";
}
