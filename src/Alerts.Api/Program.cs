namespace Alerts.Api;

public static class Program
{
    public static void Main(string[] args)
    {
        var app = BuildApplication(args);
        app.Run();
    }

    public static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        return builder.Build();
    }
}
