using System;
using System.Collections.Generic;
using GameServer.Shared.Messages.Base;
using GameServer.Shared.Messages.CardBattle;
using GameServer.Shared.Messages.Connection;
using GameServer.Shared.Messages.Fight;
using GameServer.Shared.Messages.Map;
using GameServer.Shared.Messages.Movement;
using GameServer.Shared.Models;

namespace GameServer.Shared
{
    /// <summary>
    /// Client-side game state that keeps track of player, map, fight, and card battle state.
    /// Processes incoming server messages to update the state.
    /// </summary>
    public class GameState
    {
        // Player data
        public string PlayerId { get; private set; }
        public MapPosition PlayerPosition { get; private set; }
        
        // Map data
        public string CurrentMapId { get; private set; }
        public TilemapData CurrentTilemapData { get; private set; }
        
        // Other players
        public Dictionary<string, MapPosition> OtherPlayers { get; private set; } = new Dictionary<string, MapPosition>();
        public Dictionary<string, PlayerMapInfo> OtherPlayerInfo { get; private set; } = new Dictionary<string, PlayerMapInfo>();
        public Dictionary<string, bool> PlayersInFight { get; private set; } = new Dictionary<string, bool>();
        
        // Fight state
        public string CurrentFightId { get; private set; }
        public string OpponentId { get; private set; }
        public bool IsInFight => !string.IsNullOrEmpty(CurrentFightId);
        
        // Card battle state
        public Dictionary<string, string> CardSvgData { get; private set; } = new Dictionary<string, string>();
        public List<CardInfo> CardsInHand { get; private set; } = new List<CardInfo>();
        public int PlayerHitPoints { get; private set; } = 50;
        public int PlayerActionPoints { get; private set; } = 0;
        public int PlayerDeckCount { get; private set; } = 0;
        public int PlayerDiscardPileCount { get; private set; } = 0;
        public int OpponentHitPoints { get; private set; } = 50;
        public int OpponentActionPoints { get; private set; } = 0;
        public int OpponentDeckCount { get; private set; } = 0;
        public int OpponentDiscardPileCount { get; private set; } = 0;
        public List<CardInfo> OpponentCardsInHand { get; private set; } = new List<CardInfo>();
        public List<StatusEffectInfo> PlayerStatusEffects { get; private set; } = new List<StatusEffectInfo>();
        public List<StatusEffectInfo> OpponentStatusEffects { get; private set; } = new List<StatusEffectInfo>();
        public CardInfo LastPlayedCard { get; private set; }
        public string CurrentTurnPlayerId { get; private set; }
        public bool IsPlayerTurn => CurrentTurnPlayerId == PlayerId;
        
        // Pending operations
        public MapPosition PendingMove { get; private set; }
        public bool IsMoving => PendingMove != null;

        /// <summary>
        /// Initializes a new instance of the GameState class
        /// </summary>
        public GameState() { }

        /// <summary>
        /// Processes an incoming server message and updates the game state accordingly
        /// </summary>
        /// <typeparam name="T">Type of the server message</typeparam>
        /// <param name="extServerMessage">The server message to process</param>
        /// <exception cref="NotImplementedException">Thrown when a message type is not handled</exception>
        public void OnRecieve<T>(T extServerMessage) where T: ExtServerMessage
        {
            switch(extServerMessage)
            {
                // Connection messages
                case ExtPlayerIdResponse msg:
                    OnExtPlayerIdResponse(msg);
                    break;
                
                // Map messages
                case ExtMapListResponse msg:
                    // Map list doesn't affect game state
                    break;
                case ExtJoinMapInitiated msg:
                    OnExtJoinMapInitiated(msg);
                    break;
                case ExtJoinMapCompleted msg:
                    OnExtJoinMapCompleted(msg);
                    break;
                case ExtJoinMapFailed msg:
                    // Handle join map failure
                    break;
                case ExtLeaveMapInitiated msg:
                    OnExtLeaveMapInitiated(msg);
                    break;
                case ExtLeaveMapCompleted msg:
                    OnExtLeaveMapCompleted(msg);
                    break;
                case ExtLeaveMapFailed msg:
                    // Handle leave map failure
                    break;
                case ExtPlayerJoinedMap msg:
                    OnExtPlayerJoinedMap(msg);
                    break;
                case ExtPlayerLeftMap msg:
                    OnExtPlayerLeftMap(msg);
                    break;
                case ExtPlayerPositionChange msg:
                    OnExtPlayerPositionChange(msg);
                    break;
                
                // Movement messages
                case ExtMoveInitiated msg:
                    OnExtMoveInitiated(msg);
                    break;
                case ExtMoveCompleted msg:
                    OnExtMoveCompleted(msg);
                    break;
                case ExtMoveFailed msg:
                    OnExtMoveFailed(msg);
                    break;
                
                // Fight messages
                case ExtFightChallengeReceived msg:
                    // Challenge received doesn't affect state until accepted
                    break;
                case ExtFightStarted msg:
                    OnExtFightStarted(msg);
                    break;
                case ExtFightEnded msg:
                    OnExtFightEnded(msg);
                    break;
                
                // Card battle messages
                case ExtCardImages msg:
                    OnExtCardImages(msg);
                    break;
                case ExtCardDrawn msg:
                    OnExtCardDrawn(msg);
                    break;
                case ExtTurnStarted msg:
                    OnExtTurnStarted(msg);
                    break;
                case ExtTurnEnded msg:
                    OnExtTurnEnded(msg);
                    break;
                case ExtCardPlayInitiated msg:
                    // Initiated doesn't affect state until completed
                    break;
                case ExtCardPlayCompleted msg:
                    OnExtCardPlayCompleted(msg);
                    break;
                case ExtCardPlayFailed msg:
                    OnExtCardPlayFailed(msg);
                    break;
                case ExtEffectApplied msg:
                    OnExtEffectApplied(msg);
                    break;
                case ExtFightStateUpdate msg:
                    OnExtFightStateUpdate(msg);
                    break;
                
                default:
                    throw new NotImplementedException($"Message type {extServerMessage.GetType().Name} not handled");
            };
        }

        /// <summary>
        /// Resets the game state to its initial values
        /// </summary>
        public void Reset()
        {
            // Reset map data
            CurrentMapId = null;
            CurrentTilemapData = null;
            
            // Reset player data
            PlayerPosition = null;
            PendingMove = null;
            
            // Reset other players
            OtherPlayers.Clear();
            OtherPlayerInfo.Clear();
            PlayersInFight.Clear();
            
            // Reset fight state
            CurrentFightId = null;
            OpponentId = null;
            
            // Reset card battle state
            CardSvgData.Clear();
            CardsInHand.Clear();
            PlayerHitPoints = 50;
            PlayerActionPoints = 0;
            PlayerDeckCount = 0;
            PlayerDiscardPileCount = 0;
            OpponentHitPoints = 50;
            OpponentActionPoints = 0;
            OpponentDeckCount = 0;
            OpponentDiscardPileCount = 0;
            OpponentCardsInHand.Clear();
            PlayerStatusEffects.Clear();
            OpponentStatusEffects.Clear();
            LastPlayedCard = null;
            CurrentTurnPlayerId = null;
        }

        /// <summary>
        /// Adds a player to the list of other players
        /// </summary>
        /// <param name="playerId">The ID of the player to add</param>
        /// <param name="playerInfo">Information about the player</param>
        public void AddPlayer(string playerId, PlayerMapInfo playerInfo)
        {
            if (playerId != PlayerId)
            {
                // Store the player info
                OtherPlayerInfo[playerId] = playerInfo;
                
                // Extract position for backward compatibility
                OtherPlayers[playerId] = playerInfo.Position;
                
                // If the player is in a fight, add them to the PlayersInFight dictionary
                if (!string.IsNullOrEmpty(playerInfo.FightId))
                {
                    PlayersInFight[playerId] = true;
                }
            }
        }

        /// <summary>
        /// Updates the position of a player in the list of other players
        /// </summary>
        /// <param name="playerId">The ID of the player to update</param>
        /// <param name="position">The new position of the player</param>
        public void UpdatePlayerPosition(string playerId, MapPosition position)
        {
            if (playerId != PlayerId && OtherPlayers.ContainsKey(playerId))
            {
                OtherPlayers[playerId] = position;
                
                // Also update the position in the PlayerMapInfo
                if (OtherPlayerInfo.ContainsKey(playerId))
                {
                    var currentInfo = OtherPlayerInfo[playerId];
                    var updatedInfo = new PlayerMapInfo(position, currentInfo.FightId);
                    OtherPlayerInfo[playerId] = updatedInfo;
                }
            }
        }

        /// <summary>
        /// Removes a player from the list of other players
        /// </summary>
        /// <param name="playerId">The ID of the player to remove</param>
        public void RemovePlayer(string playerId)
        {
            OtherPlayers.Remove(playerId);
            OtherPlayerInfo.Remove(playerId);
            PlayersInFight.Remove(playerId);
        }

        #region Connection Message Handlers

        private void OnExtPlayerIdResponse(ExtPlayerIdResponse msg)
        {
            PlayerId = msg.PlayerId;
        }

        #endregion

        #region Map Message Handlers

        private void OnExtJoinMapInitiated(ExtJoinMapInitiated msg)
        {
            // Store the map ID
            CurrentMapId = msg.MapId;
        }

        private void OnExtJoinMapCompleted(ExtJoinMapCompleted msg)
        {
            // Set map data
            CurrentMapId = msg.MapId;
            CurrentTilemapData = msg.TilemapData;
            
            // Set player position
            PlayerPosition = msg.Position;
            
            // Add other players
            OtherPlayers.Clear();
            OtherPlayerInfo.Clear();
            PlayersInFight.Clear();
            
            foreach (var kvp in msg.PlayerInfo)
            {
                if (kvp.Key != PlayerId)
                {
                    AddPlayer(kvp.Key, kvp.Value);
                }
            }
        }

        private void OnExtLeaveMapInitiated(ExtLeaveMapInitiated msg)
        {
            // No state changes needed
        }

        private void OnExtLeaveMapCompleted(ExtLeaveMapCompleted msg)
        {
            // Clear map data
            CurrentMapId = null;
            CurrentTilemapData = null;
            
            // Clear player position
            PlayerPosition = null;
            
            // Clear other players
            OtherPlayers.Clear();
            OtherPlayerInfo.Clear();
            PlayersInFight.Clear();
        }

        private void OnExtPlayerJoinedMap(ExtPlayerJoinedMap msg)
        {
            if (msg.PlayerId != PlayerId && msg.Position != null)
            {
                // Create player info
                var playerInfo = new PlayerMapInfo(msg.Position);
                
                // Add player to collections
                AddPlayer(msg.PlayerId, playerInfo);
            }
        }

        private void OnExtPlayerLeftMap(ExtPlayerLeftMap msg)
        {
            if (msg.PlayerId != PlayerId)
            {
                RemovePlayer(msg.PlayerId);
            }
        }

        private void OnExtPlayerPositionChange(ExtPlayerPositionChange msg)
        {
            if (msg.PlayerId == PlayerId && msg.Position != null)
            {
                // Update player position
                PlayerPosition = msg.Position;
            }
            else if (msg.Position != null)
            {
                // Update other player position
                UpdatePlayerPosition(msg.PlayerId, msg.Position);
            }
        }

        #endregion

        #region Movement Message Handlers

        private void OnExtMoveInitiated(ExtMoveInitiated msg)
        {
            PendingMove = msg.NewPosition;
        }

        private void OnExtMoveCompleted(ExtMoveCompleted msg)
        {
            PlayerPosition = msg.NewPosition;
            PendingMove = null;
        }

        private void OnExtMoveFailed(ExtMoveFailed msg)
        {
            PendingMove = null;
        }

        #endregion

        #region Fight Message Handlers

        private void OnExtFightStarted(ExtFightStarted msg)
        {
            // Generate a fight ID
            string fightId = $"fight_{msg.Player1Id}_{msg.Player2Id}";
            
            // Mark both players as in a fight
            PlayersInFight[msg.Player1Id] = true;
            PlayersInFight[msg.Player2Id] = true;
            
            // Update the fight ID in OtherPlayerInfo for both players
            if (OtherPlayerInfo.ContainsKey(msg.Player1Id))
            {
                var currentInfo = OtherPlayerInfo[msg.Player1Id];
                var updatedInfo = new PlayerMapInfo(currentInfo.Position, fightId);
                OtherPlayerInfo[msg.Player1Id] = updatedInfo;
            }
            
            if (OtherPlayerInfo.ContainsKey(msg.Player2Id))
            {
                var currentInfo = OtherPlayerInfo[msg.Player2Id];
                var updatedInfo = new PlayerMapInfo(currentInfo.Position, fightId);
                OtherPlayerInfo[msg.Player2Id] = updatedInfo;
            }
            
            // Set fight state for the main player if they're involved
            if (msg.Player1Id == PlayerId || msg.Player2Id == PlayerId)
            {
                CurrentFightId = fightId;
                OpponentId = msg.Player1Id == PlayerId ? msg.Player2Id : msg.Player1Id;
                
                // Reset card battle state for new fight
                CardSvgData.Clear();
                CardsInHand.Clear();
                PlayerHitPoints = 50;
                PlayerActionPoints = 0;
                PlayerDeckCount = 0;
                PlayerDiscardPileCount = 0;
                OpponentHitPoints = 50;
                OpponentActionPoints = 0;
                OpponentDeckCount = 0;
                OpponentDiscardPileCount = 0;
                OpponentCardsInHand.Clear();
                PlayerStatusEffects.Clear();
                OpponentStatusEffects.Clear();
                CurrentTurnPlayerId = null;
            }
        }

        private void OnExtFightEnded(ExtFightEnded msg)
        {
            // Special handling for disconnection
            if (msg.Reason == "Player disconnected")
            {
                // Remove the disconnected player from the PlayersInFight dictionary
                PlayersInFight.Remove(msg.LoserId);
                
                // Also remove from OtherPlayers and OtherPlayerInfo
                OtherPlayers.Remove(msg.LoserId);
                OtherPlayerInfo.Remove(msg.LoserId);
            }
            else
            {
                // Normal cleanup for both players
                PlayersInFight.Remove(msg.WinnerId);
                PlayersInFight.Remove(msg.LoserId);
                
                // Clear fight IDs in OtherPlayerInfo
                if (OtherPlayerInfo.ContainsKey(msg.WinnerId))
                {
                    var currentInfo = OtherPlayerInfo[msg.WinnerId];
                    var updatedInfo = new PlayerMapInfo(currentInfo.Position, null);
                    OtherPlayerInfo[msg.WinnerId] = updatedInfo;
                }
                
                if (OtherPlayerInfo.ContainsKey(msg.LoserId))
                {
                    var currentInfo = OtherPlayerInfo[msg.LoserId];
                    var updatedInfo = new PlayerMapInfo(currentInfo.Position, null);
                    OtherPlayerInfo[msg.LoserId] = updatedInfo;
                }
            }
            
            // Reset fight state if the main player was involved
            if (msg.WinnerId == PlayerId || msg.LoserId == PlayerId)
            {
                CurrentFightId = null;
                OpponentId = null;
                
                // Reset card battle state
                CardSvgData.Clear();
                CardsInHand.Clear();
                PlayerHitPoints = 50;
                PlayerActionPoints = 0;
                PlayerDeckCount = 0;
                PlayerDiscardPileCount = 0;
                OpponentHitPoints = 50;
                OpponentActionPoints = 0;
                OpponentDeckCount = 0;
                OpponentDiscardPileCount = 0;
                OpponentCardsInHand.Clear();
                PlayerStatusEffects.Clear();
                OpponentStatusEffects.Clear();
                CurrentTurnPlayerId = null;
            }
        }

        #endregion

        #region Card Battle Message Handlers

        private void OnExtCardImages(ExtCardImages msg)
        {
            foreach (var entry in msg.CardSvgData)
            {
                CardSvgData[entry.Key] = entry.Value;
            }
        }

        private void OnExtCardDrawn(ExtCardDrawn msg)
        {
            // Only update the SVG data cache, don't modify the hand
            string cardId = msg.CardInfo.Id;
            CardSvgData[cardId] = msg.SvgData;
            
            // Hand updates are now handled by OnExtFightStateUpdate
        }

        private void OnExtTurnStarted(ExtTurnStarted msg)
        {
            CurrentTurnPlayerId = msg.ActivePlayerId;
            
            // Card handling is now managed by OnExtFightStateUpdate
        }

        private void OnExtTurnEnded(ExtTurnEnded msg)
        {
            if (msg.PlayerId == PlayerId)
            {
                // Player's turn ended
                CurrentTurnPlayerId = OpponentId;
                
                // Clear player's hand as it's moved to discard pile
                CardsInHand.Clear();
            }
            else
            {
                // Opponent's turn ended
                CurrentTurnPlayerId = PlayerId;
                
                // Clear opponent's hand as it's moved to discard pile
                OpponentCardsInHand.Clear();
            }
        }

        private void OnExtCardPlayCompleted(ExtCardPlayCompleted msg)
        {
            string cardId = msg.PlayedCard.Id;
            
            // Store the last played card
            LastPlayedCard = msg.PlayedCard;
            
            if (msg.PlayerId == PlayerId)
            {
                // Remove the card from the player's hand
                for (int i = 0; i < CardsInHand.Count; i++)
                {
                    if (CardsInHand[i].Id == cardId)
                    {
                        CardsInHand.RemoveAt(i);
                        break;
                    }
                }
            }
            else
            {
                // Remove the card from the opponent's hand
                for (int i = 0; i < OpponentCardsInHand.Count; i++)
                {
                    if (OpponentCardsInHand[i].Id == cardId)
                    {
                        OpponentCardsInHand.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void OnExtCardPlayFailed(ExtCardPlayFailed msg)
        {
            // Handle card play failure (log or other handling)
        }

        private void OnExtEffectApplied(ExtEffectApplied msg)
        {
            // Update player stats based on effect
            if (msg.TargetPlayerId == PlayerId)
            {
                if (msg.EffectType == "Damage")
                {
                    PlayerHitPoints = Math.Max(0, PlayerHitPoints - msg.Value);
                }
                else if (msg.EffectType == "Heal")
                {
                    PlayerHitPoints = Math.Min(50, PlayerHitPoints + msg.Value);
                }
            }
            else
            {
                if (msg.EffectType == "Damage")
                {
                    OpponentHitPoints = Math.Max(0, OpponentHitPoints - msg.Value);
                }
                else if (msg.EffectType == "Heal")
                {
                    OpponentHitPoints = Math.Min(50, OpponentHitPoints + msg.Value);
                }
            }
        }

        private void OnExtFightStateUpdate(ExtFightStateUpdate msg)
        {
            CurrentTurnPlayerId = msg.CurrentTurnPlayerId;
            
            // Determine which state belongs to the player and which to the opponent
            string playerStateId = msg.PlayerState.PlayerId;
            string opponentStateId = msg.OpponentState.PlayerId;
            
            // Set player and opponent states based on player ID
            if (playerStateId == PlayerId)
            {
                SetPlayerState(msg.PlayerState);
                SetOpponentState(msg.OpponentState);
            }
            else
            {
                SetPlayerState(msg.OpponentState);
                SetOpponentState(msg.PlayerState);
            }
        }

        private void SetPlayerState(PlayerFightStateDto state)
        {
            PlayerHitPoints = state.HitPoints;
            PlayerActionPoints = state.ActionPoints;
            PlayerDeckCount = state.DeckCount;
            PlayerDiscardPileCount = state.DiscardPileCount;
            
            // Update player hand
            CardsInHand.Clear();
            foreach (var card in state.Hand)
            {
                CardsInHand.Add(card);
            }
            
            // Update player status effects
            PlayerStatusEffects.Clear();
            foreach (var effect in state.StatusEffects)
            {
                PlayerStatusEffects.Add(effect);
            }
        }

        private void SetOpponentState(PlayerFightStateDto state)
        {
            OpponentHitPoints = state.HitPoints;
            OpponentActionPoints = state.ActionPoints;
            OpponentDeckCount = state.DeckCount;
            OpponentDiscardPileCount = state.DiscardPileCount;
            
            // Update opponent hand
            OpponentCardsInHand.Clear();
            foreach (var card in state.Hand)
            {
                OpponentCardsInHand.Add(card);
            }
            
            // Update opponent status effects
            OpponentStatusEffects.Clear();
            foreach (var effect in state.StatusEffects)
            {
                OpponentStatusEffects.Add(effect);
            }
        }

        #endregion
    }
}
