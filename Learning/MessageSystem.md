# Card Battle Game Messaging System: Technical Details for LLMs

## 1. Message Architecture

The game uses a bidirectional messaging system between client and server:

```
Client (Godot) <--WebSocket--> Server (C# Actor System)
```

All communication is asynchronous and event-driven, with messages serialized as JSON.

## 2. Message Type Hierarchy

### Base Message Types

```
- BaseMessage
  ├── FromClientMessage (client → server)
  └── ToClientMessage (server → client)
```

### Message Naming Conventions

- `In*` prefix: Messages from client to server (e.g., `InJoinMap`)
- `Out*` prefix: Messages from server to client (e.g., `OutJoinMapCompleted`)

### Internal Actor Messages

The server also uses internal messages between actors:
- `CreatePlayer`: GameActor → PlayerActor
- `JoinMap`: PlayerActor → MapActor
- `FightStarted`: FightActor → PlayerActor

## 3. Message Flow Patterns

### Request-Response Pattern

```
Client                Server
  │                     │
  │─── InJoinMap ─────>│
  │                     │
  │<── OutJoinMapInitiated ──│
  │                     │
  │<── OutJoinMapCompleted ──│
```

### Broadcast Pattern

```
Client A    Server    Client B
   │          │          │
   │          │<─────────│ InPlayerMove
   │          │          │
   │<─────────┼─────────>│ OutPlayerPositionChange
```

### State Update Pattern

```
Client                Server
   │                    │
   │─── InPlayCard ───>│
   │                    │
   │<── OutCardPlayCompleted ──│
   │                    │
   │<── OutEffectApplied ─────│
   │                    │
   │<── OutFightStateUpdate ──│
```

## 4. Message Processing Pipeline

### Client-Side Processing

1. Client sends a message via `NetworkManager.SendMessage<T>()`
2. Message is serialized to JSON and sent over WebSocket
3. Server processes the message and sends response(s)
4. Client receives message in `NetworkManager._Process()`
5. Message is deserialized and dispatched via pattern matching
6. Appropriate signal is emitted to notify game components
7. Game state is updated based on the message

### Server-Side Processing

1. Server receives message in `GameSession.OnWsReceived()`
2. Message is deserialized and forwarded to the player's actor
3. `PlayerActor.ReceiveAsync()` processes the message
4. Message is dispatched to appropriate handler method
5. Handler updates state and may send messages to other actors
6. Response messages are sent back to client via `_sendToClient` delegate

## 5. Key Message Types and Their Purpose

### Connection Messages
- `OutConnectionConfirmed`: Confirms client connection and provides player ID

### Map Navigation Messages
- `InRequestMapList`: Client requests available maps
- `OutRequestMapListResponse`: Server provides list of available maps
- `InJoinMap`: Client requests to join a specific map
- `OutJoinMapCompleted`: Server confirms map join with map data
- `OutPlayerJoinedMap`: Notifies other players about a new player

### Movement Messages
- `InPlayerMove`: Client requests to move to a new position
- `OutMoveInitiated`: Server acknowledges movement request
- `OutMoveCompleted`: Server confirms movement was successful
- `OutPlayerPositionChange`: Notifies other players about position change

### Fight Messages
- `InFightChallengeSend`: Client challenges another player to a fight
- `OutFightChallengeReceived`: Server notifies target player about challenge
- `OutFightStarted`: Server notifies both players that fight has started

### Card Battle Messages
- `OutCardImages`: Provides SVG data for card rendering
- `OutCardDrawn`: Notifies player about a newly drawn card
- `InPlayCard`: Client requests to play a specific card
- `OutCardPlayCompleted`: Server confirms card was played
- `OutEffectApplied`: Notifies about card effect application
- `OutFightStateUpdate`: Provides complete fight state update
- `InEndTurn`: Client requests to end their turn
- `OutTurnStarted`/`OutTurnEnded`: Notifies about turn changes

## 6. State Synchronization

The server maintains the authoritative state, while clients maintain a local representation:

### Server → Client Synchronization
- Initial state sent on connection/map join
- Incremental updates sent after state changes
- Complete state updates sent periodically (fight state)

### Client → Server Validation
- Client sends action requests (move, play card)
- Server validates requests against current state
- Server accepts or rejects requests
- Client updates local state based on server response

## 7. Error Handling

The system uses explicit error messages for failed operations:
- `OutJoinMapFailed`: Map join failed (e.g., map full)
- `OutMoveFailed`: Movement failed (e.g., obstacle)
- `OutCardPlayFailed`: Card play failed (e.g., not enough action points)

Errors include descriptive messages to explain the failure reason.

## 8. Optimizations

### Message Size Reduction
- Only essential data is sent in messages
- Card SVG data is cached on the client
- Complete state updates are only sent when necessary

### Batching
- Multiple card images sent in a single message
- Fight state updates include all relevant data in one message

## 9. Reasoning About Message Flow for LLMs

When analyzing this system, consider:

1. **Message Causality**: Each message is a response to a previous message or event
2. **State Transitions**: Messages trigger state changes on both client and server
3. **Validation Chain**: Client requests → Server validation → Success/failure response
4. **Broadcast Propagation**: Actions by one client affect all connected clients
5. **Asynchronous Nature**: Messages may arrive out of order or be delayed

Understanding these patterns will help you reason about the system's behavior and potential edge cases.
