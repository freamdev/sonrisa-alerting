using System.Net;
using Alerts.Api;
using Alerts.Api.Infrastructure.News;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Alerts.UnitTests;

public sealed class DevelopmentNewsTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public async Task ActualHostMapsMockOnlyInDevelopment(string environment, bool expected)
    {
        await using var app = Program.BuildApplication(["--environment", environment]);
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>().Select(x => x.RoutePattern.RawText);
        Assert.Equal(expected, routes.Contains("/dev/mock-news"));
    }

    [Fact]
    public async Task FixtureIsReloadedAndMissingFixtureReturns503()
    {
        var fixture = Path.Combine(Path.GetTempPath(), $"alerts-news-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(fixture, "[]");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.WebHost.UseTestServer();
            builder.Services.Configure<NewsSourceOptions>(x => x.FixturePath = fixture);
            await using var app = builder.Build();
            app.MapDevelopmentNews();
            await app.StartAsync();
            using var client = app.GetTestClient();
            Assert.Equal("[]", await client.GetStringAsync("/dev/mock-news"));
            await File.WriteAllTextAsync(fixture, "[null]");
            Assert.Equal("[null]", await client.GetStringAsync("/dev/mock-news"));
            File.Delete(fixture);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/dev/mock-news")).StatusCode);
            await app.StopAsync();
        }
        finally { if (File.Exists(fixture)) File.Delete(fixture); }
    }

    [Theory]
    [InlineData("NewsSource:Endpoint", "file:///tmp/news")]
    [InlineData("NewsSource:IntervalSeconds", "0")]
    [InlineData("NewsSource:TimeoutSeconds", "121")]
    public async Task BadPollingConfigurationIsRejected(string key, string value)
    {
        await using var app = Program.BuildApplication(["--NewsSource:Enabled=true", $"--{key}={value}"]);
        Assert.Throws<OptionsValidationException>(() => app.Services.GetRequiredService<IOptions<NewsSourceOptions>>().Value);
    }
}
