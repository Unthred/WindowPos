using Microsoft.Extensions.Logging;

namespace WindowPos;

public static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("TrayApplicationContext", LogLevel.Debug)
                .AddProvider(new FileLoggerProvider("log.txt"));
        });

        var logger = loggerFactory.CreateLogger<TrayApplicationContext>();
        Application.Run(new TrayApplicationContext(logger));
    }
}