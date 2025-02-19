using System.Collections.Generic;
using System.Linq;

namespace GameServer.Application.Models
{
    public static class CardLibrary
    {
        // Attack Cards (15)
        public static readonly Card[] AttackCards = new[]
        {
            // Direct Damage
            new Card("atk_001", "Fireball", CardType.Attack, CardSubtype.DirectDamage, 2, "Deal 4 damage to target"),
            new Card("atk_002", "Lightning Strike", CardType.Attack, CardSubtype.DirectDamage, 3, "Deal 6 damage to target"),
            new Card("atk_003", "Ice Shard", CardType.Attack, CardSubtype.DirectDamage, 1, "Deal 2 damage and slow target"),
            
            // Area of Effect
            new Card("atk_004", "Earthquake", CardType.Attack, CardSubtype.AreaOfEffect, 4, "Deal 3 damage to all enemies"),
            new Card("atk_005", "Meteor Shower", CardType.Attack, CardSubtype.AreaOfEffect, 5, "Deal 4 damage to all enemies"),
            new Card("atk_006", "Chain Lightning", CardType.Attack, CardSubtype.AreaOfEffect, 3, "Deal 2 damage to three random targets"),
            
            // Piercing
            new Card("atk_007", "Armor Pierce", CardType.Attack, CardSubtype.Piercing, 2, "Deal 3 damage, ignoring shields"),
            new Card("atk_008", "Shadow Strike", CardType.Attack, CardSubtype.Piercing, 3, "Deal 5 damage, ignoring shields"),
            new Card("atk_009", "Void Blade", CardType.Attack, CardSubtype.Piercing, 4, "Deal 7 damage, ignoring shields"),
            
            // Vampiric
            new Card("atk_010", "Life Drain", CardType.Attack, CardSubtype.Vampiric, 3, "Deal 3 damage and heal for the amount"),
            new Card("atk_011", "Soul Siphon", CardType.Attack, CardSubtype.Vampiric, 4, "Deal 5 damage and heal for the amount"),
            new Card("atk_012", "Blood Leech", CardType.Attack, CardSubtype.Vampiric, 2, "Deal 2 damage and heal for double the amount"),
            
            // Combo
            new Card("atk_013", "Quick Strike", CardType.Attack, CardSubtype.Combo, 1, "Deal 2 damage, +2 if played after another attack"),
            new Card("atk_014", "Double Slash", CardType.Attack, CardSubtype.Combo, 2, "Deal 3 damage, +3 if played after another attack"),
            new Card("atk_015", "Triple Thrust", CardType.Attack, CardSubtype.Combo, 3, "Deal 4 damage, +4 if played after another attack")
        };

        // Defense Cards (15)
        public static readonly Card[] DefenseCards = new[]
        {
            // Shield
            new Card("def_001", "Iron Shield", CardType.Defense, CardSubtype.Shield, 2, "Reduce next damage by 4"),
            new Card("def_002", "Magic Barrier", CardType.Defense, CardSubtype.Shield, 3, "Reduce next damage by 6"),
            new Card("def_003", "Energy Shield", CardType.Defense, CardSubtype.Shield, 4, "Reduce all damage by 2 for 2 turns"),
            
            // Redirect
            new Card("def_004", "Mirror Shield", CardType.Defense, CardSubtype.Redirect, 3, "Return 50% of next damage taken"),
            new Card("def_005", "Reflection Ward", CardType.Defense, CardSubtype.Redirect, 4, "Return 100% of next damage taken"),
            new Card("def_006", "Spell Bounce", CardType.Defense, CardSubtype.Redirect, 5, "Return next spell with 50% more damage"),
            
            // Heal
            new Card("def_007", "Minor Heal", CardType.Defense, CardSubtype.Heal, 2, "Restore 4 health"),
            new Card("def_008", "Major Heal", CardType.Defense, CardSubtype.Heal, 4, "Restore 8 health"),
            new Card("def_009", "Regeneration", CardType.Defense, CardSubtype.Heal, 3, "Restore 2 health per turn for 3 turns"),
            
            // Dodge
            new Card("def_010", "Quick Step", CardType.Defense, CardSubtype.Dodge, 1, "50% chance to dodge next attack"),
            new Card("def_011", "Smoke Screen", CardType.Defense, CardSubtype.Dodge, 3, "75% chance to dodge next 2 attacks"),
            new Card("def_012", "Phase Shift", CardType.Defense, CardSubtype.Dodge, 4, "100% chance to dodge next attack"),
            
            // Fortify
            new Card("def_013", "Steel Skin", CardType.Defense, CardSubtype.Fortify, 3, "Increase defense by 2 for 3 turns"),
            new Card("def_014", "Diamond Shell", CardType.Defense, CardSubtype.Fortify, 4, "Increase defense by 3 for 3 turns"),
            new Card("def_015", "Adamantine Aura", CardType.Defense, CardSubtype.Fortify, 5, "Increase defense by 4 for 3 turns")
        };

        // Utility Cards (10)
        public static readonly Card[] UtilityCards = new[]
        {
            // Draw
            new Card("utl_001", "Strategic Planning", CardType.Utility, CardSubtype.Draw, 2, "Draw 2 cards"),
            new Card("utl_002", "Deep Insight", CardType.Utility, CardSubtype.Draw, 4, "Draw 3 cards"),
            
            // Energy Boost
            new Card("utl_003", "Energy Surge", CardType.Utility, CardSubtype.EnergyBoost, 0, "Gain 2 energy this turn"),
            new Card("utl_004", "Mana Crystal", CardType.Utility, CardSubtype.EnergyBoost, 2, "Gain 3 energy this turn"),
            
            // Discard
            new Card("utl_005", "Mind Drain", CardType.Utility, CardSubtype.Discard, 3, "Opponent discards 2 random cards"),
            new Card("utl_006", "Memory Wipe", CardType.Utility, CardSubtype.Discard, 5, "Opponent discards 3 random cards"),
            
            // Lock
            new Card("utl_007", "Seal Magic", CardType.Utility, CardSubtype.Lock, 3, "Block opponent's special cards for 2 turns"),
            new Card("utl_008", "Silence", CardType.Utility, CardSubtype.Lock, 4, "Block opponent's utility cards for 2 turns"),
            
            // Transform
            new Card("utl_009", "Transmute", CardType.Utility, CardSubtype.Transform, 2, "Transform a card in hand into a random card"),
            new Card("utl_010", "Polymorph", CardType.Utility, CardSubtype.Transform, 4, "Transform target card into a basic attack card")
        };

        // Special Cards (5)
        public static readonly Card[] SpecialCards = new[]
        {
            // One of each subtype
            new Card("spc_001", "Dragon's Breath", CardType.Special, CardSubtype.Ultimate, 8, "Deal 12 damage to all enemies and apply burn"),
            new Card("spc_002", "Storm Field", CardType.Special, CardSubtype.Environment, 6, "All players take 2 damage per turn and spells cost +1"),
            new Card("spc_003", "Phoenix Ally", CardType.Special, CardSubtype.Summon, 7, "Summon a Phoenix that deals 3 damage per turn"),
            new Card("spc_004", "Doom Mark", CardType.Special, CardSubtype.Curse, 5, "Target takes 2 additional damage from all sources"),
            new Card("spc_005", "Element Merge", CardType.Special, CardSubtype.Fusion, 4, "Combine 2 cards in hand, adding their effects together")
        };

        public static IEnumerable<Card> AllCards =>
            AttackCards.Concat(DefenseCards).Concat(UtilityCards).Concat(SpecialCards);
    }
}
