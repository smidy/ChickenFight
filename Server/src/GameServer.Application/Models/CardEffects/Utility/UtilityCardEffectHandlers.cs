using Proto;
using GameServer.Shared.Messages.CardBattle;

namespace GameServer.Application.Models.CardEffects.Utility
{
    /// <summary>
    /// Base class for all Utility card effect handlers
    /// </summary>
    public class UtilityCardEffectHandler : DefaultCardEffectHandler
    {
        /// <summary>
        /// Calculates the base action point gain for a utility card
        /// </summary>
        protected virtual int CalculateBaseActionPoints(Card card)
        {
            return (int)Math.Ceiling(card.Cost / 2.0);
        }
    }

    /// <summary>
    /// Handler for Draw utility cards that allow drawing additional cards
    /// </summary>
    public class DrawEffectHandler : UtilityCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Calculate number of cards to draw based on card cost
            int cardsToDraw = Math.Max(1, card.Cost / 2);
            
            // Get the player's deck from the context
            // In a real implementation, we would need to access the player's deck
            // For now, we'll just send a notification
            
            // Create notification
            var notification = new ExtEffectApplied(playerId, "DrawCards", cardsToDraw, card.Name);
            
            return new EffectResult(
                $"Drew {cardsToDraw} cards",
                new[] { notification }
            );
        }
    }

    /// <summary>
    /// Handler for EnergyBoost utility cards that provide additional action points
    /// </summary>
    public class EnergyBoostEffectHandler : UtilityCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Energy boost provides enhanced action points now and in future turns
            int baseActionPoints = CalculateBaseActionPoints(card);
            int bonusActionPoints = card.Cost / 2; // Bonus AP based on card cost
            int totalActionPoints = baseActionPoints + bonusActionPoints;
            int duration = 3; // Effect lasts for 3 turns
            int futureActionPoints = card.Cost / 2; // Action points granted in future turns
            
            // Get player state and add immediate action points
            var playerState = state.GetPlayerState(playerId);
            playerState.ActionPoints += totalActionPoints;
            
            // Create and apply action point boost status effect for future turns
            var apBoostEffect = new StatusEffect(
                $"apboost_{Guid.NewGuid()}",
                "Energy Boost",
                $"Grants {futureActionPoints} additional action points each turn",
                duration,
                StatusEffectType.ActionPointBoost,
                futureActionPoints,
                card.Name
            );
            
            state.ApplyStatusEffect(playerId, apBoostEffect);
            
            // Create notifications
            var immediateNotification = new ExtEffectApplied(playerId, "ActionPoints", totalActionPoints, card.Name);
            var boostNotification = new ExtEffectApplied(playerId, "ActionPointBoost", futureActionPoints, card.Name);
            
            return new EffectResult(
                $"Gained {totalActionPoints} action points and +{futureActionPoints} action points per turn for {duration} turns",
                new[] { immediateNotification, boostNotification }
            );
        }
    }

    /// <summary>
    /// Handler for Discard utility cards that force the opponent to discard cards
    /// </summary>
    public class DiscardEffectHandler : UtilityCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Calculate number of cards the opponent must discard
            int cardsToDiscard = Math.Max(1, card.Cost / 2);
            
            // In a real implementation, we would force the opponent to discard cards
            // For now, we'll just send a notification
            
            // Create notification
            var notification = new ExtEffectApplied(targetId, "ForceDiscard", cardsToDiscard, card.Name);
            
            return new EffectResult(
                $"Forced opponent to discard {cardsToDiscard} cards",
                new[] { notification }
            );
        }
    }

    /// <summary>
    /// Handler for Lock utility cards that prevent the opponent from playing certain cards
    /// </summary>
    public class LockEffectHandler : UtilityCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Lock prevents the opponent from playing cards of a certain type
            int lockDuration = card.Cost;
            
            // Create and apply card lock status effect
            var lockEffect = new StatusEffect(
                $"lock_{Guid.NewGuid()}",
                "Card Lock",
                $"Prevents playing certain card types for {lockDuration} turns",
                lockDuration,
                StatusEffectType.CardLock,
                1, // Magnitude doesn't matter for this effect
                card.Name
            );
            
            state.ApplyStatusEffect(targetId, lockEffect);
            
            // Create notification
            var notification = new ExtEffectApplied(targetId, "CardLock", lockDuration, card.Name);
            
            return new EffectResult(
                $"Locked opponent's cards for {lockDuration} turns",
                new[] { notification }
            );
        }
    }

    /// <summary>
    /// Handler for Transform utility cards that transform cards in hand
    /// </summary>
    public class TransformEffectHandler : UtilityCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Transform would change cards in the player's hand
            // In a full implementation, this would modify the player's hand
            int cardsToTransform = Math.Max(1, card.Cost / 2);
            
            // Create notification
            var notification = new ExtEffectApplied(playerId, "TransformCards", cardsToTransform, card.Name);
            
            return new EffectResult(
                $"Transformed {cardsToTransform} cards in hand",
                new[] { notification }
            );
        }
    }
}
