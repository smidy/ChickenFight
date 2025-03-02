using Proto;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Models.CardEffects.Special
{
    /// <summary>
    /// Base class for all Special card effect handlers
    /// </summary>
    public class SpecialCardEffectHandler : DefaultCardEffectHandler
    {
        /// <summary>
        /// Calculates the base effect power for a special card
        /// </summary>
        protected virtual int CalculateBaseEffectPower(Card card)
        {
            return card.Cost * 3; // Special cards have stronger effects
        }
    }

    /// <summary>
    /// Handler for Ultimate special cards that have powerful, game-changing effects
    /// </summary>
    public class UltimateEffectHandler : SpecialCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Ultimate cards have powerful effects that can change the course of the game
            int effectPower = CalculateBaseEffectPower(card);
            int damage = effectPower;
            int healing = effectPower / 2;
            int duration = 3; // Effects last for 3 turns
            
            // Apply immediate effects
            state.ApplyDamage(targetId, damage);
            state.ApplyHealing(playerId, healing);
            
            // Create and apply ongoing effects
            
            // 1. Damage boost effect for player
            var damageBoostEffect = new StatusEffect(
                $"ultimate_dmgboost_{Guid.NewGuid()}",
                "Ultimate: Damage Boost",
                $"Increases damage dealt by 50%",
                duration,
                StatusEffectType.DamageBoost,
                50, // 50% damage boost
                card.Name
            );
            
            state.ApplyStatusEffect(playerId, damageBoostEffect);
            
            // 2. Action point boost effect for player
            var apBoostEffect = new StatusEffect(
                $"ultimate_ap_{Guid.NewGuid()}",
                "Ultimate: Action Point Boost",
                $"Grants 1 additional action point each turn",
                duration,
                StatusEffectType.ActionPointBoost,
                1,
                card.Name
            );
            
            state.ApplyStatusEffect(playerId, apBoostEffect);
            
            // Create notifications
            var damageNotification = new OutEffectApplied(targetId, "UltimateDamage", damage, card.Name);
            var healNotification = new OutEffectApplied(playerId, "UltimateHeal", healing, card.Name);
            var boostNotification = new OutEffectApplied(playerId, "UltimatePowerup", duration, card.Name);
            
            return new EffectResult(
                $"Ultimate attack! Dealt {damage} damage, healed for {healing}, and gained powerful boosts for {duration} turns",
                new[] { damageNotification, healNotification, boostNotification }
            );
        }
    }

    /// <summary>
    /// Handler for Environment special cards that affect the battle environment
    /// </summary>
    public class EnvironmentEffectHandler : SpecialCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Environment cards change the rules of the battle
            int effectPower = CalculateBaseEffectPower(card) / 3;
            int duration = card.Cost;
            
            // Create and apply environment status effect
            var environmentEffect = new StatusEffect(
                $"environment_{Guid.NewGuid()}",
                "Environment Change",
                $"Changes battle rules for {duration} turns",
                duration,
                StatusEffectType.EnvironmentEffect,
                effectPower,
                card.Name
            );
            
            // Apply to both players
            state.ApplyStatusEffect(playerId, environmentEffect);
            state.ApplyStatusEffect(targetId, environmentEffect);
            
            // Create notification
            var notification = new OutEffectApplied("global", "EnvironmentChange", effectPower, card.Name);
            
            return new EffectResult(
                $"Changed battle environment for {duration} turns",
                new[] { notification }
            );
        }
    }

    /// <summary>
    /// Handler for Summon special cards that summon allies to assist in battle
    /// </summary>
    public class SummonEffectHandler : SpecialCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Summon cards create an ally that attacks each turn
            int effectPower = CalculateBaseEffectPower(card) / 3;
            int summonDamage = effectPower;
            int duration = card.Cost;
            
            // Apply immediate damage
            state.ApplyDamage(targetId, summonDamage);
            
            // Create and apply summon status effect (damage over time to opponent)
            var summonEffect = new StatusEffect(
                $"summon_{Guid.NewGuid()}",
                "Summoned Ally",
                $"Deals {summonDamage} damage each turn",
                duration,
                StatusEffectType.DamageOverTime,
                summonDamage,
                card.Name
            );
            
            state.ApplyStatusEffect(targetId, summonEffect);
            
            // Create notifications
            var damageNotification = new OutEffectApplied(targetId, "SummonDamage", summonDamage, card.Name);
            var summonNotification = new OutEffectApplied(playerId, "SummonCreated", duration, card.Name);
            
            return new EffectResult(
                $"Summoned an ally that dealt {summonDamage} damage and will deal {summonDamage} damage each turn for {duration} turns",
                new[] { damageNotification, summonNotification }
            );
        }
    }

    /// <summary>
    /// Handler for Curse special cards that apply negative effects to the opponent
    /// </summary>
    public class CurseEffectHandler : SpecialCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Curse cards apply negative effects to the opponent over time
            int effectPower = CalculateBaseEffectPower(card) / 3;
            int curseDamage = effectPower;
            int duration = card.Cost;
            
            // Apply immediate damage
            state.ApplyDamage(targetId, curseDamage);
            
            // Create and apply curse status effect (damage over time)
            var curseEffect = new StatusEffect(
                $"curse_{Guid.NewGuid()}",
                "Curse",
                $"Deals {curseDamage / 2} damage each turn",
                duration,
                StatusEffectType.DamageOverTime,
                curseDamage / 2, // Ongoing damage is half the initial damage
                card.Name
            );
            
            state.ApplyStatusEffect(targetId, curseEffect);
            
            // Create notifications
            var damageNotification = new OutEffectApplied(targetId, "CurseDamage", curseDamage, card.Name);
            var curseNotification = new OutEffectApplied(targetId, "CurseApplied", duration, card.Name);
            
            return new EffectResult(
                $"Applied a curse that dealt {curseDamage} damage and will deal {curseDamage / 2} damage each turn for {duration} turns",
                new[] { damageNotification, curseNotification }
            );
        }
    }

    /// <summary>
    /// Handler for Fusion special cards that combine effects of multiple card types
    /// </summary>
    public class FusionEffectHandler : SpecialCardEffectHandler
    {
        public override EffectResult ApplyEffect(IContext context, FightState state, string playerId, string targetId, Card card)
        {
            // Fusion cards combine effects of multiple card types
            int effectPower = CalculateBaseEffectPower(card) / 2;
            int damage = effectPower;
            int healing = effectPower / 2;
            int actionPoints = card.Cost / 2;
            int duration = 2; // Effects last for 2 turns
            
            // Apply immediate effects
            state.ApplyDamage(targetId, damage);
            state.ApplyHealing(playerId, healing);
            state.GetPlayerState(playerId).ActionPoints += actionPoints;
            
            // Create and apply ongoing effects
            
            // 1. Damage over time effect on opponent
            var dotEffect = new StatusEffect(
                $"fusion_dot_{Guid.NewGuid()}",
                "Fusion: Damage Over Time",
                $"Deals {damage / 3} damage each turn",
                duration,
                StatusEffectType.DamageOverTime,
                damage / 3,
                card.Name
            );
            
            state.ApplyStatusEffect(targetId, dotEffect);
            
            // 2. Healing over time effect on player
            var hotEffect = new StatusEffect(
                $"fusion_hot_{Guid.NewGuid()}",
                "Fusion: Healing Over Time",
                $"Heals for {healing / 3} each turn",
                duration,
                StatusEffectType.HealOverTime,
                healing / 3,
                card.Name
            );
            
            state.ApplyStatusEffect(playerId, hotEffect);
            
            // 3. Action point boost effect on player
            var apBoostEffect = new StatusEffect(
                $"fusion_ap_{Guid.NewGuid()}",
                "Fusion: Action Point Boost",
                $"Grants {actionPoints / 2} additional action points each turn",
                duration,
                StatusEffectType.ActionPointBoost,
                actionPoints / 2,
                card.Name
            );
            
            state.ApplyStatusEffect(playerId, apBoostEffect);
            
            // Create notifications
            var damageNotification = new OutEffectApplied(targetId, "FusionDamage", damage, card.Name);
            var healNotification = new OutEffectApplied(playerId, "FusionHeal", healing, card.Name);
            var apNotification = new OutEffectApplied(playerId, "FusionActionPoints", actionPoints, card.Name);
            
            return new EffectResult(
                $"Fusion effect! Dealt {damage} damage, healed for {healing}, gained {actionPoints} action points, " +
                $"and applied ongoing effects for {duration} turns",
                new[] { damageNotification, healNotification, apNotification }
            );
        }
    }
}
