using System.Collections.Generic;
using System.Linq;

namespace GameServer.Application.Models
{
    public class Deck
    {
        private readonly List<Card> _cards;
        public IReadOnlyList<Card> Cards => _cards.AsReadOnly();
        
        public Deck()
        {
            _cards = new List<Card>();
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
        
        public void Clear()
        {
            _cards.Clear();
        }
    }
}
