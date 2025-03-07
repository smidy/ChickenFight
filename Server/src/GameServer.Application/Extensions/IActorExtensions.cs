using GameServer.Infrastructure;
using Proto;

namespace GameServer.Application.Extensions
{
    /// <summary>
    /// Somewhat hacky convenience extension for logging because I'm not using DI for this solution
    /// </summary>
    public static class IActorExtensions
    {
        public static void LogInformation(this IActor actor, string template, params object[] args)
        {
            var actorName = actor.GetType().Name;
            LoggingService.Logger.Information($"[{actorName}] {template}", args);
        }

        public static void LogDebug(this IActor actor, string template, params object[] args)
        {
            var actorName = actor.GetType().Name;
            LoggingService.Logger.Debug($"[{actorName}] {template}", args);
        }

        public static void LogWarning(this IActor actor, string template, params object[] args)
        {
            var actorName = actor.GetType().Name;
            LoggingService.Logger.Warning($"[{actorName}] {template}", args);
        }

        public static void LogError(this IActor actor, string template, params object[] args)
        {
            var actorName = actor.GetType().Name;
            LoggingService.Logger.Error($"[{actorName}] {template}", args);
        }

        public static void LogError(this IActor actor, Exception excp, string template, params object[] args)
        {
            var actorName = actor.GetType().Name;
            LoggingService.Logger.Error($"[{actorName}] {excp.ToString()}", args);
        }
    }
}
