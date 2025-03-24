namespace GameServer.Shared.Messages.CardBattle
{
    /// <summary>
    /// Information about a card for client display
    /// </summary>
    public class CardInfo
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Cost { get; }

        public CardInfo(string id, string name, string description, int cost)
        {
            Id = id;
            Name = name;
            Description = description;
            Cost = cost;
        }
    }
    
    /// <summary>
    /// Information about a status effect for client display
    /// </summary>
    public class StatusEffectInfo
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Duration { get; }
        public string Type { get; }
        public int Magnitude { get; }

        public StatusEffectInfo(string id, string name, string description, int duration, string type, int magnitude)
        {
            Id = id;
            Name = name;
            Description = description;
            Duration = duration;
            Type = type;
            Magnitude = magnitude;
        }
    }
    
    /// <summary>
    /// Represents a player's current fight status
    /// </summary>
    public class PlayerFightState
    {
        public string PlayerId { get; }
        public int HitPoints { get; }
        public int ActionPoints { get; }
        public System.Collections.Generic.List<CardInfo> Hand { get; }
        public int DeckCount { get; }
        public int DiscardPileCount { get; }
        public System.Collections.Generic.List<StatusEffectInfo> StatusEffects { get; }

        public PlayerFightState(
            string playerId,
            int hitPoints,
            int actionPoints,
            System.Collections.Generic.List<CardInfo> hand,
            int deckCount,
            int discardPileCount,
            System.Collections.Generic.List<StatusEffectInfo> statusEffects = null)
        {
            PlayerId = playerId;
            HitPoints = hitPoints;
            ActionPoints = actionPoints;
            Hand = hand;
            DeckCount = deckCount;
            DiscardPileCount = discardPileCount;
            StatusEffects = statusEffects ?? new System.Collections.Generic.List<StatusEffectInfo>();
        }
    }
}
