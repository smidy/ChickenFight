using GameServer.Shared.Models;

namespace GameServer.Shared.ExternalMessages
{
    public delegate Task SendToClientDelegate<in T>(T message) where T : BaseExternalMessage;

    public record BaseExternalMessage
    {

    }

    // Connection messages
    public record ExtConnectionConfirmed(string PlayerId) : BaseExternalMessage;

    // Map listing messages
    public record ExtRequestMapList() : BaseExternalMessage;
    public record RequestMapListResponse(List<MapInfo> Maps) : BaseExternalMessage;
    public record MapInfo(string Id, string Name, int Width, int Height, int PlayerCount);

    // Map join/leave messages
    public record ExtJoinMap(string MapId) : BaseExternalMessage;

    public record ExtJoinMapInitiated(string MapId) : BaseExternalMessage;

    public record ExtJoinMapCompleted(string MapId, string PlayerId, Position Position, TilemapData TilemapData, IReadOnlyDictionary<string, Position> PlayerPositions) : BaseExternalMessage;

    public record ExtJoinMapFailed(string MapId, string Error) : BaseExternalMessage;

    public record ExtPlayerJoinedMap(string PlayerId, Position? Position) : BaseExternalMessage;

    public record ExtPlayerPositionChange(string PlayerId, Position? Position) : BaseExternalMessage;

    public record ExtPlayerLeftMap(string PlayerId) : BaseExternalMessage;

    public record ExtLeaveMap(string MapId) : BaseExternalMessage;

    public record ExtLeaveMapInitiated(string MapId) : BaseExternalMessage;

    public record ExtLeaveMapCompleted(string MapId) : BaseExternalMessage;

    public record ExtLeaveMapFailed(string MapId, string Error) : BaseExternalMessage;

    // Movement messages
    public record ExtMove(Position NewPosition) : BaseExternalMessage;

    public record ExtMoveInitiated(Position NewPosition) : BaseExternalMessage;

    public record ExtMoveCompleted(Position NewPosition) : BaseExternalMessage;

    public record ExtMoveFailed(Position AttemptedPosition, string Error) : BaseExternalMessage;

    // State update messages
    public record PlayerState(string Id, string Name, Position Position);
    public record ExtPlayerInfo(PlayerState? State) : BaseExternalMessage;

    // Map data
    public record TilemapData(int Width, int Height, int[] TileData);

    // External messages for client communication
    /// <summary>Sent when a player initiates a fight challenge</summary>
    public record ExtFightChallengeSend(string TargetId) : BaseExternalMessage;
    
    /// <summary>Received by a player when they are challenged to a fight</summary>
    public record ExtFightChallengeReceived(string ChallengerId) : BaseExternalMessage;

    /// <summary>Sent when a player accepts a fight challenge</summary>
    public record ExtFightChallengeAccepted(string TargetId) : BaseExternalMessage;

    /// <summary>Notifies players that a fight has begun</summary>
    public record ExtFightStarted(string OpponentId) : BaseExternalMessage;

    /// <summary>Notifies players that a fight has ended with a winner</summary>
    public record ExtFightEnded(string WinnerId, string Reason) : BaseExternalMessage;

    // Card battle system messages
    /// <summary>Contains essential information about a card for client display</summary>
    public record CardInfo(string Id, string Name, string Description, int Cost) : BaseExternalMessage;
    
    /// <summary>
    /// Represents a player's current fight status including HP, AP, cards in hand,
    /// and remaining deck size
    /// </summary>
    public record ExPlayerFightState(
        int HitPoints,
        int ActionPoints,
        List<CardInfo> Hand,
        int DeckCount
    ) : BaseExternalMessage;

    /// <summary>
    /// Provides a complete update of the fight state for both players,
    /// sent after any state-changing action
    /// </summary>
    public record ExtFightStateUpdate(
        string CurrentTurnPlayerId,
        ExPlayerFightState PlayerState,
        ExPlayerFightState OpponentState
    ) : BaseExternalMessage;

    /// <summary>
    /// Notifies that a player's turn has begun and shows their newly drawn cards
    /// </summary>
    public record ExtTurnStarted(
        string ActivePlayerId,
        List<CardInfo> DrawnCards
    ) : BaseExternalMessage;

    /// <summary>Notifies that a player's turn has ended</summary>
    public record ExtTurnEnded(string PlayerId) : BaseExternalMessage;

    /// <summary>Request from client to play a specific card</summary>
    public record ExtPlayCard(string CardId) : BaseExternalMessage;
    
    /// <summary>Confirms that a card play attempt has been received</summary>
    public record ExtCardPlayInitiated(string CardId) : BaseExternalMessage;
    
    /// <summary>
    /// Notifies that a card was successfully played and describes its effect
    /// </summary>
    public record ExtCardPlayCompleted(
        string PlayerId,
        CardInfo PlayedCard,
        string Effect
    ) : BaseExternalMessage;
    
    /// <summary>
    /// Notifies that a card play attempt failed and provides the reason
    /// </summary>
    public record ExtCardPlayFailed(
        string CardId,
        string Error
    ) : BaseExternalMessage;

    /// <summary>
    /// Notifies that a card effect was applied to a player
    /// (e.g. damage, healing, status effects)
    /// </summary>
    public record ExtEffectApplied(
        string TargetPlayerId,
        string EffectType,
        int Value,
        string Source
    ) : BaseExternalMessage;
}
