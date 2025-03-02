using Proto;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Models.CardEffects.Attack
{
    /// <summary>
    /// Base class for all Attack card effect handlers
    /// </summary>
    public class AttackCardEffectHandler : DefaultCardEffectHandler
    {
        /// <summary>
        /// Calculates the base damage for an attack card
        /// </summary>
        protected virtual int CalculateBaseDamage(Card card)
        {
            return card.Cost * 2;
        }
    }

    /// <summary>
    /// Handler for DirectDamage attack cards that deal straightforward damage to a single target
    /// </summary>
    public class DirectDamageEffectHandler : AttackCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Calculate damage with a bonus for direct damage cards
            int baseDamage = CalculateBaseDamage(card);
            int bonusDamage = card.Cost; // Direct damage gets a bonus equal to the card's cost
            int totalDamage = baseDamage + bonusDamage;
            
            // Apply damage to target
            state.ApplyDamage(targetId, totalDamage);
            
            // Create notification
            var notification = new OutEffectApplied(targetId, "Damage", totalDamage, card.Name);
            
            return new EffectResult(
                $"Dealt {totalDamage} direct damage",
                new[] { notification }
            );
        }
    }

    /// <summary>
    /// Handler for AreaOfEffect attack cards that deal damage to all enemies (currently just one opponent)
    /// </summary>
    public class AreaOfEffectHandler : AttackCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Area damage is slightly less than direct damage but affects all enemies
            // In the current 1v1 implementation, this is just one opponent
            int baseDamage = CalculateBaseDamage(card);
            int reducedDamage = (int)(baseDamage * 0.8); // 80% of normal damage
            
            // Apply damage to target
            state.ApplyDamage(targetId, reducedDamage);
            
            // Create notification
            var notification = new OutEffectApplied(targetId, "AreaDamage", reducedDamage, card.Name);
            
            return new EffectResult(
                $"Dealt {reducedDamage} area damage",
                new[] { notification }
            );
        }
    }

    /// <summary>
    /// Handler for Piercing attack cards that ignore some defense
    /// </summary>
    public class PiercingHandler : AttackCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Piercing damage is the same as normal damage but would ignore defense if implemented
            int damage = CalculateBaseDamage(card);
            
            // Apply damage to target
            state.ApplyDamage(targetId, damage);
            
            // Create notification
            var notification = new OutEffectApplied(targetId, "PiercingDamage", damage, card.Name);
            
            return new EffectResult(
                $"Dealt {damage} piercing damage",
                new[] { notification }
            );
        }
    }

    /// <summary>
    /// Handler for Vampiric attack cards that deal damage and heal the player
    /// </summary>
    public class VampiricHandler : AttackCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Vampiric attacks deal less damage but heal the player for a portion of the damage
            int baseDamage = CalculateBaseDamage(card);
            int reducedDamage = (int)(baseDamage * 0.7); // 70% of normal damage
            int healing = reducedDamage / 2; // Heal for half the damage dealt
            
            // Apply effects
            state.ApplyDamage(targetId, reducedDamage);
            state.ApplyHealing(playerId, healing);
            
            // Create notifications
            var damageNotification = new OutEffectApplied(targetId, "VampiricDamage", reducedDamage, card.Name);
            var healNotification = new OutEffectApplied(playerId, "VampiricHeal", healing, card.Name);
            
            return new EffectResult(
                $"Dealt {reducedDamage} vampiric damage and healed for {healing}",
                new[] { damageNotification, healNotification }
            );
        }
    }

    /// <summary>
    /// Handler for Combo attack cards that deal more damage if played after another attack
    /// </summary>
    public class ComboHandler : AttackCardEffectHandler
    {
        // In a real implementation, we would track the last card played
        // For now, we'll just assume the combo always activates
        
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Combo attacks deal more damage when they follow another attack
            int baseDamage = CalculateBaseDamage(card);
            int comboDamage = baseDamage + card.Cost; // Add card cost as bonus damage
            
            // Apply damage to target
            state.ApplyDamage(targetId, comboDamage);
            
            // Create notification
            var notification = new OutEffectApplied(targetId, "ComboDamage", comboDamage, card.Name);
            
            return new EffectResult(
                $"Dealt {comboDamage} combo damage",
                new[] { notification }
            );
        }
    }
}
