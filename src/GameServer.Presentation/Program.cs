using System;
using System.Threading.Tasks;
using Proto;
using GameServer.Application.Actors;

namespace GameServer.Presentation
{
    public class Program
    {
        public static async Task Main()
        {
            try
            {
                var system = new ActorSystem();
                var gameProps = Props.FromProducer(() => new GameActor());
                var gameActor = system.Root.Spawn(gameProps);

                var server = new GameWebSocketServer(system, gameActor, "127.0.0.1", 8080);
                Console.WriteLine("Game server starting...");

                if (server.Start())
                {
                    Console.WriteLine($"Game server is listening on port {server.Port}");
                    Console.WriteLine("Press Enter to stop the server or '?' to restart the server");

                    for (;;)
                    {
                        string? line = Console.ReadLine();
                        if (string.IsNullOrEmpty(line))
                            break;

                        // Restart the server
                        if (line == "?")
                        {
                            Console.Write("Server restarting...");
                            server.Restart();
                            Console.WriteLine("Done!");
                            continue;
                        }
                    }

                    // Stop the server
                    Console.Write("Server stopping...");
                    server.Stop();
                    Console.WriteLine("Done!");
                }
                else
                {
                    Console.WriteLine("Game server failed to start");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
