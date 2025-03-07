using Serilog;
using Serilog.Events;

namespace GameServer.Infrastructure
{
    public static class LoggingService
    {
        public static ILogger Logger { get; set; } = new LoggerConfiguration()
            .WriteTo.File("logs.txt", LogEventLevel.Information, rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }
}
