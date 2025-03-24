using GameServer.Shared.Messages.Base;
using GameServer.Shared.Messages.CardBattle;
using System.Collections.Generic;

namespace GameServer.Shared.Messages.CardBattle
{
    /// <summary>
    /// Server notification with SVG data for multiple cards
    /// </summary>
    public class CardImages : ServerMessage, INotification
    {
        public Dictionary<string, string> CardSvgData { get; }

        public CardImages(Dictionary<string, string> cardSvgData) : base()
        {
            CardSvgData = cardSvgData;
        }
    }

    /// <summary>
    /// Server notification about a newly drawn card
    /// </summary>
    public class CardDrawn : ServerMessage, INotification
    {
        public CardInfo CardInfo { get; }
        public string SvgData { get; }

        public CardDrawn(CardInfo cardInfo, string svgData) : base()
        {
            CardInfo = cardInfo;
            SvgData = svgData;
        }
    }

    /// <summary>
    /// Server notification with complete fight state update
    /// </summary>
    public class FightStateUpdate : ServerMessage, INotification
    {
        public string CurrentTurnPlayerId { get; }
        public PlayerFightState PlayerState { get; }
        public PlayerFightState OpponentState { get; }

        public FightStateUpdate(
            string currentTurnPlayerId,
            PlayerFightState playerState,
            PlayerFightState opponentState) : base()
        {
            CurrentTurnPlayerId = currentTurnPlayerId;
            PlayerState = playerState;
            OpponentState = opponentState;
        }
    }

    /// <summary>
    /// Server notification that a turn has started
    /// </summary>
    public class TurnStarted : ServerMessage, INotification, IPlayerRelated
    {
        public string ActivePlayerId { get; }
        public string PlayerId => ActivePlayerId;

        public TurnStarted(string activePlayerId) : base()
        {
            ActivePlayerId = activePlayerId;
        }
    }

    /// <summary>
    /// Server notification that a turn has ended
    /// </summary>
    public class TurnEnded : ServerMessage, INotification, IPlayerRelated
    {
        public string PlayerId { get; }

        public TurnEnded(string playerId) : base()
        {
            PlayerId = playerId;
        }
    }

    /// <summary>
    /// Client request to play a card
    /// </summary>
    public class PlayCardRequest : ClientMessage, IRequest
    {
        public string CardId { get; }

        public PlayCardRequest(string cardId) : base()
        {
            CardId = cardId;
        }
    }

    /// <summary>
    /// Server notification that card play process has started
    /// </summary>
    public class CardPlayInitiated : ServerMessage, INotification
    {
        public string CardId { get; }

        public CardPlayInitiated(string cardId) : base()
        {
            CardId = cardId;
        }
    }

    /// <summary>
    /// Server notification that card play has completed successfully
    /// </summary>
    public class CardPlayCompleted : ServerMessage, IResponse, IPlayerRelated
    {
        public string PlayerId { get; }
        public CardInfo PlayedCard { get; }
        public string Effect { get; }
        public bool IsVisible { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public CardPlayCompleted(
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
    public class CardPlayFailed : ServerMessage, IResponse
    {
        public string CardId { get; }
        public bool Success => false;
        public string ErrorMessage { get; }

        public CardPlayFailed(string cardId, string error) : base()
        {
            CardId = cardId;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Server notification that a card effect was applied
    /// </summary>
    public class EffectApplied : ServerMessage, INotification, IPlayerRelated
    {
        public string TargetPlayerId { get; }
        public string EffectType { get; }
        public int Value { get; }
        public string Source { get; }
        public string PlayerId => TargetPlayerId;

        public EffectApplied(
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
    public class EndTurnRequest : ClientMessage, IRequest
    {
        public EndTurnRequest() : base() { }
    }
}
