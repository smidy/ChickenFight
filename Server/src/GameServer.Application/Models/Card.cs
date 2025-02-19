namespace GameServer.Application.Models
{
    public class Card
    {
        public string Id { get; }
        public string Name { get; }
        public CardType Type { get; }
        public CardSubtype Subtype { get; }
        public int Cost { get; }
        public string Description { get; }
        
        public Card(string id, string name, CardType type, CardSubtype subtype, int cost, string description)
        {
            Id = id;
            Name = name;
            Type = type;
            Subtype = subtype;
            Cost = cost;
            Description = description;
        }
    }

    public enum CardType
    {
        Attack,
        Defense,
        Utility,
        Special
    }

    public enum CardSubtype
    {
        // Attack subtypes
        DirectDamage,
        AreaOfEffect,
        Piercing,
        Vampiric,
        Combo,
        
        // Defense subtypes
        Shield,
        Redirect,
        Heal,
        Dodge,
        Fortify,
        
        // Utility subtypes
        Draw,
        EnergyBoost,
        Discard,
        Lock,
        Transform,
        
        // Special subtypes
        Ultimate,
        Environment,
        Summon,
        Curse,
        Fusion
    }
}
