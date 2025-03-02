using GameServer.Application.Models.CardEffects.Attack;
using GameServer.Application.Models.CardEffects.Defense;
using GameServer.Application.Models.CardEffects.Utility;
using GameServer.Application.Models.CardEffects.Special;

namespace GameServer.Application.Models.CardEffects
{
    /// <summary>
    /// Handles registration of all card effect handlers with the factory
    /// </summary>
    public static class CardEffectHandlerRegistration
    {
        /// <summary>
        /// Registers all card effect handlers with the provided registry
        /// </summary>
        /// <param name="registry">The registry to register handlers with</param>
        public static void RegisterAllHandlers(CardEffectHandlerRegistry registry)
        {
            // Register default handler for each card type
            registry.RegisterFallbackHandler(CardType.Attack, () => new AttackCardEffectHandler());
            registry.RegisterFallbackHandler(CardType.Defense, () => new DefenseCardEffectHandler());
            registry.RegisterFallbackHandler(CardType.Utility, () => new UtilityCardEffectHandler());
            registry.RegisterFallbackHandler(CardType.Special, () => new SpecialCardEffectHandler());
            
            // Register Attack card subtype handlers
            registry.RegisterHandler(CardType.Attack, CardSubtype.DirectDamage, () => new DirectDamageEffectHandler());
            registry.RegisterHandler(CardType.Attack, CardSubtype.AreaOfEffect, () => new AreaOfEffectHandler());
            registry.RegisterHandler(CardType.Attack, CardSubtype.Piercing, () => new PiercingHandler());
            registry.RegisterHandler(CardType.Attack, CardSubtype.Vampiric, () => new VampiricHandler());
            registry.RegisterHandler(CardType.Attack, CardSubtype.Combo, () => new ComboHandler());
            
            // Register Defense card subtype handlers
            registry.RegisterHandler(CardType.Defense, CardSubtype.Shield, () => new ShieldEffectHandler());
            registry.RegisterHandler(CardType.Defense, CardSubtype.Redirect, () => new RedirectEffectHandler());
            registry.RegisterHandler(CardType.Defense, CardSubtype.Heal, () => new HealEffectHandler());
            registry.RegisterHandler(CardType.Defense, CardSubtype.Dodge, () => new DodgeEffectHandler());
            registry.RegisterHandler(CardType.Defense, CardSubtype.Fortify, () => new FortifyEffectHandler());
            
            // Register Utility card subtype handlers
            registry.RegisterHandler(CardType.Utility, CardSubtype.Draw, () => new DrawEffectHandler());
            registry.RegisterHandler(CardType.Utility, CardSubtype.EnergyBoost, () => new EnergyBoostEffectHandler());
            registry.RegisterHandler(CardType.Utility, CardSubtype.Discard, () => new DiscardEffectHandler());
            registry.RegisterHandler(CardType.Utility, CardSubtype.Lock, () => new LockEffectHandler());
            registry.RegisterHandler(CardType.Utility, CardSubtype.Transform, () => new TransformEffectHandler());
            
            // Register Special card subtype handlers
            registry.RegisterHandler(CardType.Special, CardSubtype.Ultimate, () => new UltimateEffectHandler());
            registry.RegisterHandler(CardType.Special, CardSubtype.Environment, () => new EnvironmentEffectHandler());
            registry.RegisterHandler(CardType.Special, CardSubtype.Summon, () => new SummonEffectHandler());
            registry.RegisterHandler(CardType.Special, CardSubtype.Curse, () => new CurseEffectHandler());
            registry.RegisterHandler(CardType.Special, CardSubtype.Fusion, () => new FusionEffectHandler());
        }
    }
}
