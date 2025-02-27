using GameServer.Shared.Models;


namespace GameServer.Shared.ExternalMessages
{
    public abstract record ToClientMessage();

    public abstract record FromClientMessage();

    // Connection messages
    public record OutConnectionConfirmed(string PlayerId) : ToClientMessage;

    // Map listing messages
    public record InRequestMapList() : FromClientMessage;
    public record OutRequestMapListResponse(List<MapInfo> Maps) : ToClientMessage;
    public record MapInfo(string Id, string Name, int Width, int Height, int PlayerCount);

    // Map join/leave messages
    public record InJoinMap(string MapId) : FromClientMessage;

    public record InLeaveMap(string MapId) : FromClientMessage;

    public record OutJoinMapInitiated(string MapId) : ToClientMessage;

    public record OutJoinMapCompleted(string MapId, string PlayerId, MapPosition Position, TilemapData TilemapData, IReadOnlyDictionary<string, MapPosition> PlayerPositions) : ToClientMessage;

    public record OutJoinMapFailed(string MapId, string Error) : ToClientMessage;

    public record OutPlayerJoinedMap(string PlayerId, MapPosition? Position) : ToClientMessage;

    public record OutPlayerPositionChange(string PlayerId, MapPosition? Position) : ToClientMessage;

    public record OutPlayerLeftMap(string PlayerId) : ToClientMessage;

    public record OutLeaveMapInitiated(string MapId) : ToClientMessage;

    public record OutLeaveMapCompleted(string MapId) : ToClientMessage;

    public record OutLeaveMapFailed(string MapId, string Error) : ToClientMessage;

    // Movement messages
    public record InPlayerMove(MapPosition NewPosition) : FromClientMessage;

    public record OutMoveInitiated(MapPosition NewPosition) : ToClientMessage;

    public record OutMoveCompleted(MapPosition NewPosition) : ToClientMessage;

    public record OutMoveFailed(MapPosition AttemptedPosition, string Error) : ToClientMessage;

    // State update messages
    public record PlayerState(string Id, string Name, MapPosition Position);
    public record OutPlayerInfo(PlayerState? State) : ToClientMessage;

    // Map data
    public record TilemapData(int Width, int Height, int[] TileData);

    /// <summary>Sent when a player initiates a fight challenge</summary>
    public record InFightChallengeSend(string TargetId) : FromClientMessage;
    
    /// <summary>Received by a player when they are challenged to a fight</summary>
    public record OutFightChallengeReceived(string ChallengerId) : ToClientMessage;

    /// <summary>Sent when a player accepts a fight challenge</summary>
    public record InFightChallengeAccepted(string TargetId) : FromClientMessage;

    /// <summary>Notifies players that a fight has begun</summary>
    public record OutFightStarted(string OpponentId) : ToClientMessage;

    /// <summary>Notifies players that a fight has ended with a winner</summary>
    public record OutFightEnded(string WinnerId, string Reason) : ToClientMessage;

    // Card battle system messages
    /// <summary>Contains essential information about a card for client display</summary>
    public record CardInfo(string Id, string Name, string Description, int Cost) : ToClientMessage;
    
    /// <summary>
    /// Represents a player's current fight status including HP, AP, cards in hand,
    /// and remaining deck size
    /// </summary>
    public record ExPlayerFightState(
        int HitPoints,
        int ActionPoints,
        List<CardInfo> Hand,
        int DeckCount
    ) : ToClientMessage;

    /// <summary>
    /// Provides a complete update of the fight state for both players,
    /// sent after any state-changing action
    /// </summary>
    public record OutFightStateUpdate(
        string CurrentTurnPlayerId,
        ExPlayerFightState PlayerState,
        ExPlayerFightState OpponentState
    ) : ToClientMessage;

    /// <summary>
    /// Notifies that a player's turn has begun and shows their newly drawn cards
    /// </summary>
    public record OutTurnStarted(
        string ActivePlayerId,
        List<CardInfo> DrawnCards
    ) : ToClientMessage;

    /// <summary>Notifies that a player's turn has ended</summary>
    public record OutTurnEnded(string PlayerId) : ToClientMessage;

    /// <summary>Request from client to play a specific card</summary>
    public record InPlayCard(string CardId) : FromClientMessage;
    
    /// <summary>Confirms that a card play attempt has been received</summary>
    public record OutCardPlayInitiated(string CardId) : ToClientMessage;
    
    /// <summary>
    /// Notifies that a card was successfully played and describes its effect
    /// </summary>
    public record OutCardPlayCompleted(
        string PlayerId,
        CardInfo PlayedCard,
        string Effect
    ) : ToClientMessage;
    
    /// <summary>
    /// Notifies that a card play attempt failed and provides the reason
    /// </summary>
    public record OutCardPlayFailed(
        string CardId,
        string Error
    ) : ToClientMessage;

    /// <summary>
    /// Notifies that a card effect was applied to a player
    /// (e.g. damage, healing, status effects)
    /// </summary>
    public record OutEffectApplied(
        string TargetPlayerId,
        string EffectType,
        int Value,
        string Source
    ) : ToClientMessage;
    
    /// <summary>
    /// Sends SVG data for multiple cards to the client
    /// </summary>
    public record OutCardImages(
        Dictionary<string, string> CardSvgData
    ) : ToClientMessage;
    
    /// <summary>
    /// Notifies when a new card is drawn with its SVG data
    /// </summary>
    public record OutCardDrawn(
        CardInfo CardInfo,
        string SvgData
    ) : ToClientMessage;
    
    /// <summary>
    /// Request from client to end their turn
    /// </summary>
    public record InEndTurn() : FromClientMessage;
}
