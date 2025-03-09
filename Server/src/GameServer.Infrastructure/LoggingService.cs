using Serilog;
using Serilog.Events;

namespace GameServer.Infrastructure
{
    public static class LoggingService
    {
        public static ILogger Logger { get; private set; } 

        static LoggingService()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            Logger = new LoggerConfiguration().MinimumLevel.Debug()
            .WriteTo.Console(LogEventLevel.Information)
            .WriteTo.File($"log{timestamp}.txt", LogEventLevel.Information)
            .CreateLogger();
        }
    }
}
