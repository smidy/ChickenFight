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
- MessageBase
  ├── ClientMessage (client → server)
  └── ServerMessage (server → client)
```

### Message Organization

Messages are organized by domain in separate files:
- `Base`: Base message classes and interfaces
- `Connection`: Connection-related messages
- `Map`: Map navigation messages
- `Movement`: Player movement messages
- `Fight`: Fight challenge messages
- `CardBattle`: Card battle gameplay messages
- `State`: State update messages

### Message Interfaces

Messages implement interfaces that define their purpose and common properties:
- `IRequest`: Messages that represent requests from client to server
- `IResponse`: Messages that represent responses to requests (with Success/Error properties)
- `INotification`: Messages that represent notifications (no response expected)
- `IMapRelated`: Messages related to a specific map
- `IPlayerRelated`: Messages related to a specific player

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
  │─── JoinMapRequest ─────>│
  │                     │
  │<── JoinMapInitiated ──│
  │                     │
  │<── JoinMapCompleted ──│
```

### Broadcast Pattern

```
Client A    Server    Client B
   │          │          │
   │          │<─────────│ PlayerMoveRequest
   │          │          │
   │<─────────┼─────────>│ PlayerPositionChange
```

### State Update Pattern

```
Client                Server
   │                    │
   │─── PlayCardRequest ───>│
   │                    │
   │<── CardPlayCompleted ──│
   │                    │
   │<── EffectApplied ─────│
   │                    │
   │<── FightStateUpdate ──│
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
- `PlayerIdRequest`: Client requests connection confirmation
- `PlayerIdResponse`: Server confirms connection and provides player ID

### Map Navigation Messages
- `MapListRequest`: Client requests available maps
- `MapListResponse`: Server provides list of available maps
- `JoinMapRequest`: Client requests to join a specific map
- `JoinMapCompleted`: Server confirms map join with map data
- `PlayerJoinedMap`: Notifies other players about a new player

### Movement Messages
- `PlayerMoveRequest`: Client requests to move to a new position
- `MoveInitiated`: Server acknowledges movement request
- `MoveCompleted`: Server confirms movement was successful
- `PlayerPositionChange`: Notifies other players about position change

### Fight Messages
- `FightChallengeRequest`: Client challenges another player to a fight
- `FightChallengeReceived`: Server notifies target player about challenge
- `FightStarted`: Server notifies all players that fight has started

### Card Battle Messages
- `CardImages`: Provides SVG data for card rendering
- `CardDrawn`: Notifies player about a newly drawn card
- `PlayCardRequest`: Client requests to play a specific card
- `CardPlayCompleted`: Server confirms card was played
- `EffectApplied`: Notifies about card effect application
- `FightStateUpdate`: Provides complete fight state update
- `EndTurnRequest`: Client requests to end their turn
- `TurnStarted`/`TurnEnded`: Notifies about turn changes

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
- `JoinMapFailed`: Map join failed (e.g., map full)
- `MoveFailed`: Movement failed (e.g., obstacle)
- `CardPlayFailed`: Card play failed (e.g., not enough action points)

All response messages implement the `IResponse` interface, which includes:
- `Success`: Boolean indicating whether the operation succeeded
- `ErrorMessage`: String containing the reason for failure (if any)

## 8. Optimizations

### Message Size Reduction
- Only essential data is sent in messages
- Card SVG data is cached on the client
- Complete state updates are only sent when necessary

### Shared Data Transfer Objects
- Common data structures are extracted into shared DTOs
- `MapInfo`: Information about a map
- `CardInfo`: Information about a card
- `StatusEffectInfo`: Information about a status effect
- `PlayerFightState`: Information about a player's fight state

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
6. **Interface Contracts**: Messages implement interfaces that define their purpose and properties

Understanding these patterns will help you reason about the system's behavior and potential edge cases.
