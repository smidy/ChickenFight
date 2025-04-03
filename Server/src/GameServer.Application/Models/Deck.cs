using System;
using System.Collections.Generic;
using System.Linq;

namespace GameServer.Application.Models
{
    /// <summary>
    /// A read-only template for a player's card collection
    /// </summary>
    public class Deck
    {
        private readonly List<Card> _cards;
        
        /// <summary>Read-only access to the deck's cards</summary>
        public IReadOnlyList<Card> Cards => _cards.AsReadOnly();
        
        /// <summary>Number of cards in the deck</summary>
        public int CardCount => _cards.Count;
        
        public Deck()
        {
            _cards = new List<Card>();
        }
        
        /// <summary>
        /// Adds a card to the deck template if space is available
        /// </summary>
        /// <param name="card">The card to add</param>
        /// <returns>True if the card was added, false if the deck is full</returns>
        public bool AddCard(Card card)
        {
            if (_cards.Count >= 25)
                return false;
                
            _cards.Add(card);
            return true;
        }
        
        /// <summary>
        /// Removes a card from the deck template by ID
        /// </summary>
        /// <param name="cardId">The ID of the card to remove</param>
        /// <returns>True if the card was removed, false if it wasn't found</returns>
        public bool RemoveCard(string cardId)
        {
            var card = _cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null)
                return false;
                
            return _cards.Remove(card);
        }
    }
}
