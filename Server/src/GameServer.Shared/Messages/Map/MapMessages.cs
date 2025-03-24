using GameServer.Shared.Messages.Base;
using GameServer.Shared.Models;
using System.Collections.Generic;

namespace GameServer.Shared.Messages.Map
{
    /// <summary>
    /// Client request for available maps
    /// </summary>
    public class ExtMapListRequest : ExtClientMessage, IExtRequest
    {
        public ExtMapListRequest() : base() { }
    }

    /// <summary>
    /// Server response with available maps
    /// </summary>
    public class ExtMapListResponse : ExtServerMessage, IExtResponse
    {
        public List<MapInfo> Maps { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public ExtMapListResponse(List<MapInfo> maps) : base()
        {
            Maps = maps;
        }
    }

    /// <summary>
    /// Client request to join a map
    /// </summary>
    public class ExtJoinMapRequest : ExtClientMessage, IExtRequest, IExtMapRelated
    {
        public string MapId { get; }

        public ExtJoinMapRequest(string mapId) : base()
        {
            MapId = mapId;
        }
    }

    /// <summary>
    /// Client request to leave a map
    /// </summary>
    public class ExtLeaveMapRequest : ExtClientMessage, IExtRequest, IExtMapRelated
    {
        public string MapId { get; }

        public ExtLeaveMapRequest(string mapId) : base()
        {
            MapId = mapId;
        }
    }

    /// <summary>
    /// Server notification that map join process has started
    /// </summary>
    public class ExtJoinMapInitiated : ExtServerMessage, IExtNotification, IExtMapRelated
    {
        public string MapId { get; }

        public ExtJoinMapInitiated(string mapId) : base()
        {
            MapId = mapId;
        }
    }

    /// <summary>
    /// Server notification that map join has completed successfully
    /// </summary>
    public class ExtJoinMapCompleted : ExtServerMessage, IExtResponse, IExtMapRelated, IExtPlayerRelated
    {
        public string MapId { get; }
        public string PlayerId { get; }
        public MapPosition Position { get; }
        public TilemapData TilemapData { get; }
        public IReadOnlyDictionary<string, PlayerMapInfo> PlayerInfo { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public ExtJoinMapCompleted(
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
    public class ExtJoinMapFailed : ExtServerMessage, IExtResponse, IExtMapRelated
    {
        public string MapId { get; }
        public bool Success => false;
        public string ErrorMessage { get; }

        public ExtJoinMapFailed(string mapId, string error) : base()
        {
            MapId = mapId;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Server notification that map leave process has started
    /// </summary>
    public class ExtLeaveMapInitiated : ExtServerMessage, IExtNotification, IExtMapRelated
    {
        public string MapId { get; }

        public ExtLeaveMapInitiated(string mapId) : base()
        {
            MapId = mapId;
        }
    }

    /// <summary>
    /// Server notification that map leave has completed successfully
    /// </summary>
    public class ExtLeaveMapCompleted : ExtServerMessage, IExtResponse, IExtMapRelated
    {
        public string MapId { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public ExtLeaveMapCompleted(string mapId) : base()
        {
            MapId = mapId;
        }
    }

    /// <summary>
    /// Server notification that map leave has failed
    /// </summary>
    public class ExtLeaveMapFailed : ExtServerMessage, IExtResponse, IExtMapRelated
    {
        public string MapId { get; }
        public bool Success => false;
        public string ErrorMessage { get; }

        public ExtLeaveMapFailed(string mapId, string error) : base()
        {
            MapId = mapId;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Server notification that a player has joined the map
    /// </summary>
    public class ExtPlayerJoinedMap : ExtServerMessage, IExtNotification, IExtPlayerRelated
    {
        public string PlayerId { get; }
        public MapPosition? Position { get; }

        public ExtPlayerJoinedMap(string playerId, MapPosition? position) : base()
        {
            PlayerId = playerId;
            Position = position;
        }
    }

    /// <summary>
    /// Server notification that a player has left the map
    /// </summary>
    public class ExtPlayerLeftMap : ExtServerMessage, IExtNotification, IExtPlayerRelated
    {
        public string PlayerId { get; }

        public ExtPlayerLeftMap(string playerId) : base()
        {
            PlayerId = playerId;
        }
    }
}
