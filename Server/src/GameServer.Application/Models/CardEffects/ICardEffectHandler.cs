using Proto;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Models.CardEffects
{
    /// <summary>
    /// Interface for handling card effects. Implementations will process specific card types and subtypes.
    /// </summary>
    public interface ICardEffectHandler
    {
        /// <summary>
        /// Applies the effect of a card to the fight state
        /// </summary>
        /// <param name="context">The actor context for sending messages</param>
        /// <param name="state">The current fight state</param>
        /// <param name="playerId">ID of the player who played the card</param>
        /// <param name="targetId">ID of the target player (usually the opponent)</param>
        /// <param name="card">The card being played</param>
        /// <returns>Result of applying the effect, including description and notifications</returns>
        EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card);
    }

    /// <summary>
    /// Represents the result of applying a card effect
    /// </summary>
    public class EffectResult
    {
        /// <summary>
        /// Human-readable description of the effect
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Notifications to be sent to clients
        /// </summary>
        public IEnumerable<ToClientMessage> Notifications { get; }
        
        public EffectResult(string description, IEnumerable<ToClientMessage> notifications)
        {
            Description = description;
            Notifications = notifications;
        }
    }
}
