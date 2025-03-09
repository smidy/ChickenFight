using Proto;
using GameServer.Application.Actors;
using Serilog;
using Microsoft.Extensions.Logging;
using GameServer.Infrastructure;

namespace GameServer.Presentation
{
    public class Program
    {
        public static async Task Main()
        {
            try
            {
                Proto.Log.SetLoggerFactory(LoggerFactory.Create(l => l.AddSerilog(LoggingService.Logger).SetMinimumLevel(LogLevel.Information)));

                var system = new ActorSystem();
                var gameProps = Props.FromProducer(() => new GameActor());
                var gameActor = system.Root.Spawn(gameProps);

                var server = new GameWebSocketServer(system, gameActor, "127.0.0.1", 8080);
                LoggingService.Logger.Information("Game server starting...");

                if (server.Start())
                {
                    LoggingService.Logger.Information("Game server is listening on port {Port}", server.Port);
                    LoggingService.Logger.Information("Press Enter to stop the server or '?' to restart the server");

                    for (;;)
                    {
                        string? line = Console.ReadLine();
                        if (string.IsNullOrEmpty(line))
                            break;

                        // Restart the server
                        if (line == "?")
                        {
                            LoggingService.Logger.Information("Server restarting...");
                            server.Restart();
                            LoggingService.Logger.Information("Server restart completed");
                            continue;
                        }
                    }

                    // Stop the server
                    LoggingService.Logger.Information("Server stopping...");
                    server.Stop();
                    LoggingService.Logger.Information("Server stopped");
                }
                else
                {
                    LoggingService.Logger.Error("Game server failed to start");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Logger.Error(ex, "Error starting game server: {ErrorMessage}", ex.Message);
            }
        }
    }
}
