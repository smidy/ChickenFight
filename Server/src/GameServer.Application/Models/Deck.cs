using System;
using System.Collections.Generic;
using System.Linq;

namespace GameServer.Application.Models
{
    /// <summary>
    /// A container for a players card collection that supports drawing and discarding cards
    /// </summary>
    public class Deck
    {
        private readonly List<Card> _cards;
        private readonly List<Card> _discardPile;
        private readonly Random _random;
        
        public IReadOnlyList<Card> Cards => _cards.AsReadOnly();
        public IReadOnlyList<Card> DiscardPile => _discardPile.AsReadOnly();
        public int RemainingCards => _cards.Count;
        
        public Deck()
        {
            _cards = new List<Card>();
            _discardPile = new List<Card>();
            _random = new Random();
        }
        
        public bool AddCard(Card card)
        {
            if (_cards.Count >= 25)
                return false;
                
            _cards.Add(card);
            return true;
        }
        
        public bool RemoveCard(string cardId)
        {
            var card = _cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null)
                return false;
                
            return _cards.Remove(card);
        }

        public List<Card> DrawCards(int count)
        {
            var drawnCards = new List<Card>();
            
            for (int i = 0; i < count; i++)
            {
                // If deck is empty, shuffle discard pile back in
                if (!_cards.Any())
                {
                    if (!_discardPile.Any())
                        break;
                        
                    ShuffleDiscardIntoDeck();
                }
                
                // Draw top card
                var card = _cards[0];
                _cards.RemoveAt(0);
                drawnCards.Add(card);
            }
            
            return drawnCards;
        }
        
        public void DiscardCard(Card card)
        {
            _discardPile.Add(card);
        }
        
        public void Shuffle()
        {
            var n = _cards.Count;
            while (n > 1)
            {
                n--;
                var k = _random.Next(n + 1);
                var temp = _cards[k];
                _cards[k] = _cards[n];
                _cards[n] = temp;
            }
        }
        
        private void ShuffleDiscardIntoDeck()
        {
            _cards.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle();
        }
        
        public void Clear()
        {
            _cards.Clear();
            _discardPile.Clear();
        }
    }
}
