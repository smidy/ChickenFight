namespace GameServer.Application.Models
{
    /// <summary>
    /// Represents a status effect that can be applied to a player during a fight
    /// </summary>
    public class StatusEffect
    {
        /// <summary>
        /// Unique identifier for the status effect
        /// </summary>
        public string Id { get; }
        
        /// <summary>
        /// Human-readable name of the status effect
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Description of what the status effect does
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Number of turns the effect will last
        /// </summary>
        public int Duration { get; private set; }
        
        /// <summary>
        /// Type of the status effect
        /// </summary>
        public StatusEffectType Type { get; }
        
        /// <summary>
        /// Magnitude of the effect (e.g., amount of damage, healing, etc.)
        /// </summary>
        public int Magnitude { get; }
        
        /// <summary>
        /// Source of the effect (e.g., card name)
        /// </summary>
        public string Source { get; }
        
        /// <summary>
        /// Creates a new status effect
        /// </summary>
        public StatusEffect(string id, string name, string description, int duration, StatusEffectType type, int magnitude, string source)
        {
            Id = id;
            Name = name;
            Description = description;
            Duration = duration;
            Type = type;
            Magnitude = magnitude;
            Source = source;
        }
        
        /// <summary>
        /// Applies the effect to the fight state
        /// </summary>
        public virtual void Apply(FightState state, string targetId)
        {
            // Base implementation applies effects based on type
            switch (Type)
            {
                case StatusEffectType.DamageOverTime:
                    state.ApplyDamage(targetId, Magnitude);
                    break;
                    
                case StatusEffectType.HealOverTime:
                    state.ApplyHealing(targetId, Magnitude);
                    break;
                    
                case StatusEffectType.MaxHealthBoost:
                    // This is applied when the effect is first created, not on tick
                    break;
                    
                case StatusEffectType.DamageReduction:
                    // This is applied when damage is calculated, not on tick
                    break;
                    
                case StatusEffectType.DodgeChance:
                    // This is applied when damage is calculated, not on tick
                    break;
                    
                case StatusEffectType.DamageReflection:
                    // This is applied when damage is calculated, not on tick
                    break;
                    
                case StatusEffectType.ActionPointBoost:
                    // Add action points at the start of the turn
                    var playerState = state.GetPlayerState(targetId);
                    playerState.ActionPoints += Magnitude;
                    break;
                    
                // Other effect types would be handled by derived classes
            }
        }
        
        /// <summary>
        /// Decrements the duration of the effect
        /// </summary>
        public void Tick()
        {
            Duration--;
        }
        
        /// <summary>
        /// Checks if the effect has expired
        /// </summary>
        public bool IsExpired => Duration <= 0;
    }
    
    /// <summary>
    /// Types of status effects
    /// </summary>
    public enum StatusEffectType
    {
        /// <summary>Deals damage at the start of each turn</summary>
        DamageOverTime,
        
        /// <summary>Heals at the start of each turn</summary>
        HealOverTime,
        
        /// <summary>Reduces incoming damage</summary>
        DamageReduction,
        
        /// <summary>Increases outgoing damage</summary>
        DamageBoost,
        
        /// <summary>Chance to avoid damage</summary>
        DodgeChance,
        
        /// <summary>Redirects some damage back to attacker</summary>
        DamageReflection,
        
        /// <summary>Prevents playing certain card types</summary>
        CardLock,
        
        /// <summary>Increases maximum health</summary>
        MaxHealthBoost,
        
        /// <summary>Grants additional action points each turn</summary>
        ActionPointBoost,
        
        /// <summary>Changes the rules of the battle</summary>
        EnvironmentEffect
    }
}
