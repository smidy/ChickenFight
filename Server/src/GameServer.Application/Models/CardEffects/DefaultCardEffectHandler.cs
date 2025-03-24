using Proto;
using GameServer.Shared.Messages.Base;
using GameServer.Shared.Messages.CardBattle;

namespace GameServer.Application.Models.CardEffects
{
    /// <summary>
    /// Default implementation of card effect handler that provides basic functionality
    /// when no specific handler is available for a card type/subtype
    /// </summary>
    public class DefaultCardEffectHandler : ICardEffectHandler
    {
        /// <summary>
        /// Applies a generic effect based on card type
        /// </summary>
        public virtual EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Provide a basic implementation based on card type
            return card.Type switch
            {
                CardType.Attack => ApplyBasicAttack(state, targetId, card),
                CardType.Defense => ApplyBasicDefense(state, playerId, card),
                CardType.Utility => ApplyBasicUtility(state, playerId, card),
                CardType.Special => ApplyBasicSpecial(state, playerId, targetId, card),
                _ => new EffectResult("Card played", Array.Empty<ExtServerMessage>())
            };
        }

        /// <summary>
        /// Applies a basic attack effect
        /// </summary>
        protected virtual EffectResult ApplyBasicAttack(FightState state, string targetId, Card card)
        {
            // Calculate damage based on card cost
            int damage = card.Cost * 2;
            
            // Apply damage to target
            state.ApplyDamage(targetId, damage);
            
            // Create notification
            var notification = new ExtEffectApplied(targetId, "Damage", damage, card.Name);
            
            return new EffectResult(
                $"Dealt {damage} damage",
                new[] { notification }
            );
        }

        /// <summary>
        /// Applies a basic defense effect
        /// </summary>
        protected virtual EffectResult ApplyBasicDefense(FightState state, string playerId, Card card)
        {
            // Calculate healing based on card cost
            int healing = card.Cost * 2;
            
            // Apply healing to player
            state.ApplyHealing(playerId, healing);
            
            // Create notification
            var notification = new ExtEffectApplied(playerId, "Heal", healing, card.Name);
            
            return new EffectResult(
                $"Healed for {healing}",
                new[] { notification }
            );
        }

        /// <summary>
        /// Applies a basic utility effect
        /// </summary>
        protected virtual EffectResult ApplyBasicUtility(FightState state, string playerId, Card card)
        {
            // Basic implementation - grant action points equal to half the card cost (rounded up)
            int actionPoints = (int)Math.Ceiling(card.Cost / 2.0);
            
            // Get player state and add action points
            var playerState = state.GetPlayerState(playerId);
            playerState.ActionPoints += actionPoints;
            
            // Create notification
            var notification = new ExtEffectApplied(playerId, "ActionPoints", actionPoints, card.Name);
            
            return new EffectResult(
                $"Gained {actionPoints} action points",
                new[] { notification }
            );
        }

        /// <summary>
        /// Applies a basic special effect
        /// </summary>
        protected virtual EffectResult ApplyBasicSpecial(FightState state, string playerId, string targetId, Card card)
        {
            // Basic implementation - deal damage to target and heal player for half that amount
            int damage = card.Cost * 2;
            int healing = damage / 2;
            
            // Apply effects
            state.ApplyDamage(targetId, damage);
            state.ApplyHealing(playerId, healing);
            
            // Create notifications
            var damageNotification = new ExtEffectApplied(targetId, "Damage", damage, card.Name);
            var healNotification = new ExtEffectApplied(playerId, "Heal", healing, card.Name);
            
            return new EffectResult(
                $"Dealt {damage} damage and healed for {healing}",
                new[] { damageNotification, healNotification }
            );
        }
    }
}
