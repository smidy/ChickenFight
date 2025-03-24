using GameServer.Shared.Messages.Base;
using GameServer.Shared.Models;
using System.Collections.Generic;

namespace GameServer.Shared.Messages.Map
{
    /// <summary>
    /// Client request for available maps
    /// </summary>
    public class MapListRequest : ClientMessage, IRequest
    {
        public MapListRequest() : base() { }
    }

    /// <summary>
    /// Server response with available maps
    /// </summary>
    public class MapListResponse : ServerMessage, IResponse
    {
        public List<MapInfo> Maps { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public MapListResponse(List<MapInfo> maps) : base()
        {
            Maps = maps;
        }
    }

    /// <summary>
    /// Client request to join a map
    /// </summary>
    public class JoinMapRequest : ClientMessage, IRequest, IMapRelated
    {
        public string MapId { get; }

        public JoinMapRequest(string mapId) : base()
        {
            MapId = mapId;
        }
    }

    /// <summary>
    /// Client request to leave a map
    /// </summary>
    public class LeaveMapRequest : ClientMessage, IRequest, IMapRelated
    {
        public string MapId { get; }

        public LeaveMapRequest(string mapId) : base()
        {
            MapId = mapId;
        }
    }

    /// <summary>
    /// Server notification that map join process has started
    /// </summary>
    public class JoinMapInitiated : ServerMessage, INotification, IMapRelated
    {
        public string MapId { get; }

        public JoinMapInitiated(string mapId) : base()
        {
            MapId = mapId;
        }
    }

    /// <summary>
    /// Server notification that map join has completed successfully
    /// </summary>
    public class JoinMapCompleted : ServerMessage, IResponse, IMapRelated, IPlayerRelated
    {
        public string MapId { get; }
        public string PlayerId { get; }
        public MapPosition Position { get; }
        public TilemapData TilemapData { get; }
        public IReadOnlyDictionary<string, PlayerMapInfo> PlayerInfo { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public JoinMapCompleted(
            string mapId, 
            string playerId, 
            MapPosition position, 
            TilemapData tilemapData, 
            IReadOnlyDictionary<string, PlayerMapInfo> playerInfo) : base()
        {
            MapId = mapId;
            PlayerId = playerId;
            Position = position;
            TilemapData = tilemapData;
            PlayerInfo = playerInfo;
        }
    }

    /// <summary>
    /// Server notification that map join has failed
    /// </summary>
    public class JoinMapFailed : ServerMessage, IResponse, IMapRelated
    {
        public string MapId { get; }
        public bool Success => false;
        public string ErrorMessage { get; }

        public JoinMapFailed(string mapId, string error) : base()
        {
            MapId = mapId;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Server notification that map leave process has started
    /// </summary>
    public class LeaveMapInitiated : ServerMessage, INotification, IMapRelated
    {
        public string MapId { get; }

        public LeaveMapInitiated(string mapId) : base()
        {
            MapId = mapId;
        }
    }

    /// <summary>
    /// Server notification that map leave has completed successfully
    /// </summary>
    public class LeaveMapCompleted : ServerMessage, IResponse, IMapRelated
    {
        public string MapId { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public LeaveMapCompleted(string mapId) : base()
        {
            MapId = mapId;
        }
    }

    /// <summary>
    /// Server notification that map leave has failed
    /// </summary>
    public class LeaveMapFailed : ServerMessage, IResponse, IMapRelated
    {
        public string MapId { get; }
        public bool Success => false;
        public string ErrorMessage { get; }

        public LeaveMapFailed(string mapId, string error) : base()
        {
            MapId = mapId;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Server notification that a player has joined the map
    /// </summary>
    public class PlayerJoinedMap : ServerMessage, INotification, IPlayerRelated
    {
        public string PlayerId { get; }
        public MapPosition? Position { get; }

        public PlayerJoinedMap(string playerId, MapPosition? position) : base()
        {
            PlayerId = playerId;
            Position = position;
        }
    }

    /// <summary>
    /// Server notification that a player has left the map
    /// </summary>
    public class PlayerLeftMap : ServerMessage, INotification, IPlayerRelated
    {
        public string PlayerId { get; }

        public PlayerLeftMap(string playerId) : base()
        {
            PlayerId = playerId;
        }
    }
}
