using Proto;
using GameServer.Shared.Messages.CardBattle;

namespace GameServer.Application.Models.CardEffects.Defense
{
    /// <summary>
    /// Base class for all Defense card effect handlers
    /// </summary>
    public class DefenseCardEffectHandler : DefaultCardEffectHandler
    {
        /// <summary>
        /// Calculates the base healing amount for a defense card
        /// </summary>
        protected virtual int CalculateBaseHealing(Card card)
        {
            return card.Cost * 2;
        }
    }

    /// <summary>
    /// Handler for Shield defense cards that provide temporary damage reduction
    /// </summary>
    public class ShieldEffectHandler : DefenseCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Shield provides both immediate healing and a damage reduction effect
            int healing = CalculateBaseHealing(card);
            int shieldAmount = card.Cost;
            int duration = 2; // Shield lasts for 2 turns
            
            // Apply healing
            state.ApplyHealing(playerId, healing);
            
            // Create and apply shield status effect
            var shieldEffect = new StatusEffect(
                $"shield_{Guid.NewGuid()}",
                "Shield",
                $"Reduces incoming damage by {shieldAmount}",
                duration,
                StatusEffectType.DamageReduction,
                shieldAmount,
                card.Name
            );
            
            state.ApplyStatusEffect(playerId, shieldEffect);
            
            // Create notifications
            var healNotification = new ExtEffectApplied(playerId, "Heal", healing, card.Name);
            var shieldNotification = new ExtEffectApplied(playerId, "Shield", shieldAmount, card.Name);
            
            return new EffectResult(
                $"Healed for {healing} and gained {shieldAmount} shield for {duration} turns",
                new[] { healNotification, shieldNotification }
            );
        }
    }

    /// <summary>
    /// Handler for Redirect defense cards that redirect some damage back to the attacker
    /// </summary>
    public class RedirectEffectHandler : DefenseCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Redirect provides less healing but also deals some damage to the opponent
            int healing = (int)(CalculateBaseHealing(card) * 0.7); // 70% of normal healing
            int redirectDamage = card.Cost;
            int duration = 2; // Redirect lasts for 2 turns
            
            // Apply healing
            state.ApplyHealing(playerId, healing);
            
            // Apply immediate damage
            state.ApplyDamage(targetId, redirectDamage);
            
            // Create and apply redirect status effect
            var redirectEffect = new StatusEffect(
                $"redirect_{Guid.NewGuid()}",
                "Damage Reflection",
                $"Reflects {redirectDamage} damage back to attacker",
                duration,
                StatusEffectType.DamageReflection,
                redirectDamage,
                card.Name
            );
            
            state.ApplyStatusEffect(playerId, redirectEffect);
            
            // Create notifications
            var healNotification = new ExtEffectApplied(playerId, "Heal", healing, card.Name);
            var damageNotification = new ExtEffectApplied(targetId, "RedirectDamage", redirectDamage, card.Name);
            var reflectNotification = new ExtEffectApplied(playerId, "DamageReflection", redirectDamage, card.Name);
            
            return new EffectResult(
                $"Healed for {healing}, redirected {redirectDamage} damage, and gained damage reflection for {duration} turns",
                new[] { healNotification, damageNotification, reflectNotification }
            );
        }
    }

    /// <summary>
    /// Handler for Heal defense cards that provide enhanced healing
    /// </summary>
    public class HealEffectHandler : DefenseCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Heal cards provide enhanced healing
            int baseHealing = CalculateBaseHealing(card);
            int bonusHealing = card.Cost; // Bonus healing equal to card cost
            int totalHealing = baseHealing + bonusHealing;
            
            // Apply healing
            state.ApplyHealing(playerId, totalHealing);
            
            // Create notification
            var notification = new ExtEffectApplied(playerId, "EnhancedHeal", totalHealing, card.Name);
            
            return new EffectResult(
                $"Healed for {totalHealing}",
                new[] { notification }
            );
        }
    }

    /// <summary>
    /// Handler for Dodge defense cards that provide a chance to avoid the next attack
    /// </summary>
    public class DodgeEffectHandler : DefenseCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Dodge provides less healing but adds a chance to avoid damage
            int healing = (int)(CalculateBaseHealing(card) * 0.6); // 60% of normal healing
            int dodgeChance = 50; // 50% chance to dodge
            int duration = 2; // Dodge effect lasts for 2 turns
            
            // Apply healing
            state.ApplyHealing(playerId, healing);
            
            // Create and apply dodge status effect
            var dodgeEffect = new StatusEffect(
                $"dodge_{Guid.NewGuid()}",
                "Dodge",
                $"Provides a {dodgeChance}% chance to avoid damage",
                duration,
                StatusEffectType.DodgeChance,
                dodgeChance,
                card.Name
            );
            
            state.ApplyStatusEffect(playerId, dodgeEffect);
            
            // Create notifications
            var healNotification = new ExtEffectApplied(playerId, "Heal", healing, card.Name);
            var dodgeNotification = new ExtEffectApplied(playerId, "DodgeChance", dodgeChance, card.Name);
            
            return new EffectResult(
                $"Healed for {healing} and gained {dodgeChance}% dodge chance for {duration} turns",
                new[] { healNotification, dodgeNotification }
            );
        }
    }

    /// <summary>
    /// Handler for Fortify defense cards that provide healing and increase max health
    /// </summary>
    public class FortifyEffectHandler : DefenseCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Fortify provides healing and increases max health
            int healing = CalculateBaseHealing(card);
            int maxHealthIncrease = card.Cost / 2;
            int duration = -1; // Permanent effect (doesn't expire)
            
            // Apply healing and increase max health
            state.IncreaseMaxHitPoints(playerId, maxHealthIncrease);
            
            // Create and apply max health status effect (for display purposes)
            var maxHealthEffect = new StatusEffect(
                $"maxhealth_{Guid.NewGuid()}",
                "Fortify",
                $"Increases maximum health by {maxHealthIncrease}",
                duration,
                StatusEffectType.MaxHealthBoost,
                maxHealthIncrease,
                card.Name
            );
            
            state.ApplyStatusEffect(playerId, maxHealthEffect);
            
            // Create notifications
            var healNotification = new ExtEffectApplied(playerId, "Heal", healing, card.Name);
            var fortifyNotification = new ExtEffectApplied(playerId, "MaxHealthIncrease", maxHealthIncrease, card.Name);
            
            return new EffectResult(
                $"Healed for {healing} and permanently increased max health by {maxHealthIncrease}",
                new[] { healNotification, fortifyNotification }
            );
        }
    }
}
