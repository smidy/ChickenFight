namespace GameServer.Application.Models.CardEffects
{
    /// <summary>
    /// Factory interface for creating card effect handlers
    /// </summary>
    public interface ICardEffectHandlerFactory
    {
        /// <summary>
        /// Creates the appropriate handler for a given card
        /// </summary>
        /// <param name="card">The card to create a handler for</param>
        /// <returns>A handler that can process the card's effects</returns>
        ICardEffectHandler CreateHandler(Card card);
    }

    /// <summary>
    /// Registry and factory for card effect handlers that manages the creation
    /// of appropriate handlers based on card type and subtype
    /// </summary>
    public class CardEffectHandlerRegistry : ICardEffectHandlerFactory
    {
        // Dictionary mapping card type and subtype to handler factory functions
        private readonly Dictionary<(CardType, CardSubtype), Func<ICardEffectHandler>> _handlers = new();
        
        // Dictionary for fallback handlers when no specific subtype handler exists
        private readonly Dictionary<CardType, Func<ICardEffectHandler>> _fallbackHandlers = new();
        
        // Default handler when no specific handler is found
        private readonly Func<ICardEffectHandler> _defaultHandler;
        
        /// <summary>
        /// Creates a new registry with a default handler
        /// </summary>
        /// <param name="defaultHandler">Factory function for the default handler</param>
        public CardEffectHandlerRegistry(Func<ICardEffectHandler> defaultHandler)
        {
            _defaultHandler = defaultHandler ?? throw new ArgumentNullException(nameof(defaultHandler));
        }
        
        /// <summary>
        /// Registers a handler for a specific card type and subtype
        /// </summary>
        public void RegisterHandler(CardType type, CardSubtype subtype, Func<ICardEffectHandler> factory)
        {
            _handlers[(type, subtype)] = factory ?? throw new ArgumentNullException(nameof(factory));
        }
        
        /// <summary>
        /// Registers a fallback handler for a card type when no subtype-specific handler exists
        /// </summary>
        public void RegisterFallbackHandler(CardType type, Func<ICardEffectHandler> factory)
        {
            _fallbackHandlers[type] = factory ?? throw new ArgumentNullException(nameof(factory));
        }
        
        /// <summary>
        /// Creates the appropriate handler for a given card
        /// </summary>
        public ICardEffectHandler CreateHandler(Card card)
        {
            // Try to get a handler specific to this card's type and subtype
            if (_handlers.TryGetValue((card.Type, card.Subtype), out var factory))
                return factory();
                
            // Fall back to a type-only handler if available
            if (_fallbackHandlers.TryGetValue(card.Type, out var fallbackFactory))
                return fallbackFactory();
                
            // Use the default handler as a last resort
            return _defaultHandler();
        }
    }
}
