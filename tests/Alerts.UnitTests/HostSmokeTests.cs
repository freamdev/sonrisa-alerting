using Alerts.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Alerts.UnitTests;

public class HostSmokeTests
{
    [Fact]
    public async Task HostBuildsWithLoggingAndCommandLineConfiguration()
    {
        await using var app = Program.BuildApplication(["--Skeleton:SmokeTest=configured"]);

        Assert.Equal("configured", app.Configuration["Skeleton:SmokeTest"]);
        Assert.NotNull(app.Services.GetRequiredService<ILoggerFactory>());
    }
}
