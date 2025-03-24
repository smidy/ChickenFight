using GameServer.Shared.Messages.Base;
using GameServer.Shared.Messages.CardBattle;
using System.Collections.Generic;

namespace GameServer.Shared.Messages.CardBattle
{
    /// <summary>
    /// Server notification with SVG data for multiple cards
    /// </summary>
    public class ExtCardImages : ExtServerMessage, IExtNotification
    {
        public Dictionary<string, string> CardSvgData { get; }

        public ExtCardImages(Dictionary<string, string> cardSvgData) : base()
        {
            CardSvgData = cardSvgData;
        }
    }

    /// <summary>
    /// Server notification about a newly drawn card
    /// </summary>
    public class ExtCardDrawn : ExtServerMessage, IExtNotification
    {
        public CardInfo CardInfo { get; }
        public string SvgData { get; }

        public ExtCardDrawn(CardInfo cardInfo, string svgData) : base()
        {
            CardInfo = cardInfo;
            SvgData = svgData;
        }
    }

    /// <summary>
    /// Server notification with complete fight state update
    /// </summary>
    public class ExtFightStateUpdate : ExtServerMessage, IExtNotification
    {
        public string CurrentTurnPlayerId { get; }
        public PlayerFightStateDto PlayerState { get; }
        public PlayerFightStateDto OpponentState { get; }

        public ExtFightStateUpdate(
            string currentTurnPlayerId,
            PlayerFightStateDto playerState,
            PlayerFightStateDto opponentState) : base()
        {
            CurrentTurnPlayerId = currentTurnPlayerId;
            PlayerState = playerState;
            OpponentState = opponentState;
        }
    }

    /// <summary>
    /// Server notification that a turn has started
    /// </summary>
    public class ExtTurnStarted : ExtServerMessage, IExtNotification, IExtPlayerRelated
    {
        public string ActivePlayerId { get; }
        public string PlayerId => ActivePlayerId;

        public ExtTurnStarted(string activePlayerId) : base()
        {
            ActivePlayerId = activePlayerId;
        }
    }

    /// <summary>
    /// Server notification that a turn has ended
    /// </summary>
    public class ExtTurnEnded : ExtServerMessage, IExtNotification, IExtPlayerRelated
    {
        public string PlayerId { get; }

        public ExtTurnEnded(string playerId) : base()
        {
            PlayerId = playerId;
        }
    }

    /// <summary>
    /// Client request to play a card
    /// </summary>
    public class ExtPlayCardRequest : ExtClientMessage, IExtRequest
    {
        public string CardId { get; }

        public ExtPlayCardRequest(string cardId) : base()
        {
            CardId = cardId;
        }
    }

    /// <summary>
    /// Server notification that card play process has started
    /// </summary>
    public class ExtCardPlayInitiated : ExtServerMessage, IExtNotification
    {
        public string CardId { get; }

        public ExtCardPlayInitiated(string cardId) : base()
        {
            CardId = cardId;
        }
    }

    /// <summary>
    /// Server notification that card play has completed successfully
    /// </summary>
    public class ExtCardPlayCompleted : ExtServerMessage, IExtResponse, IExtPlayerRelated
    {
        public string PlayerId { get; }
        public CardInfo PlayedCard { get; }
        public string Effect { get; }
        public bool IsVisible { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public ExtCardPlayCompleted(
            string playerId,
            CardInfo playedCard,
            string effect,
            bool isVisible = true) : base()
        {
            PlayerId = playerId;
            PlayedCard = playedCard;
            Effect = effect;
            IsVisible = isVisible;
        }
    }

    /// <summary>
    /// Server notification that card play has failed
    /// </summary>
    public class ExtCardPlayFailed : ExtServerMessage, IExtResponse
    {
        public string CardId { get; }
        public bool Success => false;
        public string ErrorMessage { get; }

        public ExtCardPlayFailed(string cardId, string error) : base()
        {
            CardId = cardId;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Server notification that a card effect was applied
    /// </summary>
    public class ExtEffectApplied : ExtServerMessage, IExtNotification, IExtPlayerRelated
    {
        public string TargetPlayerId { get; }
        public string EffectType { get; }
        public int Value { get; }
        public string Source { get; }
        public string PlayerId => TargetPlayerId;

        public ExtEffectApplied(
            string targetPlayerId,
            string effectType,
            int value,
            string source) : base()
        {
            TargetPlayerId = targetPlayerId;
            EffectType = effectType;
            Value = value;
            Source = source;
        }
    }

    /// <summary>
    /// Client request to end the current turn
    /// </summary>
    public class ExtEndTurnRequest : ExtClientMessage, IExtRequest
    {
        public ExtEndTurnRequest() : base() { }
    }
}
