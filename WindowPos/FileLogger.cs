using Microsoft.Extensions.Logging;

namespace WindowPos;

public class FileLogger(string path) : ILogger
{
    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        string message = formatter(state, exception);
        File.AppendAllText(path, message + Environment.NewLine);
    }
}