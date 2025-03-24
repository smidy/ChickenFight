# Card Battle Game Architecture for LLMs

## 1. System Architecture Overview

This is a multiplayer card battle game with a client-server architecture:

- **Client**: Built with Godot Engine using C# scripts
- **Server**: C# application using the Actor Model pattern
- **Communication**: WebSocket protocol with JSON-serialized messages

The system follows a message-passing architecture where clients send requests to the server, and the server responds with state updates. This design enables real-time multiplayer interactions while maintaining a single source of truth on the server.

## 2. Actor Model Implementation

The server uses the Proto.Actor framework to implement the Actor Model pattern:

- **Actors**: Independent units of computation that communicate exclusively through message passing
- **Hierarchy**:
  - GameActor (root) - Manages the overall game state
  - MapManagerActor - Manages all maps in the game
  - MapActor - Represents a single game map with players
  - PlayerActor - Represents a connected player
  - FightActor - Manages a card battle between two players

Each actor has a specific responsibility and only processes messages relevant to its domain. This isolation provides natural concurrency and fault tolerance.

## 3. Client-Server Communication

### WebSocket Protocol

- Clients connect to the server via WebSockets (default: ws://127.0.0.1:8080)
- Each client connection creates a dedicated WebSocket session on the server
- When a client connects, the server creates a PlayerActor to represent that client

### Message Organization

Messages are organized by domain in separate files:
- `Base`: Base message classes and interfaces
- `Connection`: Connection-related messages
- `Map`: Map navigation messages
- `Movement`: Player movement messages
- `Fight`: Fight challenge messages
- `CardBattle`: Card battle gameplay messages
- `State`: State update messages

### Message Format

All messages are serialized to JSON and follow these base types:
- `ClientMessage`: Base type for all client-to-server messages
- `ServerMessage`: Base type for all server-to-client messages

Messages implement interfaces that define their purpose:
- `IRequest`: Messages that represent requests from client to server
- `IResponse`: Messages that represent responses to requests
- `INotification`: Messages that represent notifications (no response expected)

Messages include:
- Connection messages (e.g., `PlayerIdResponse`)
- Map-related messages (e.g., `JoinMapRequest`, `JoinMapCompleted`)
- Movement messages (e.g., `PlayerMoveRequest`, `MoveCompleted`)
- Fight messages (e.g., `FightChallengeRequest`, `FightStarted`)
- Card battle messages (e.g., `PlayCardRequest`, `CardPlayCompleted`)

### Message Flow Example

1. Client sends `JoinMapRequest` to join a specific map
2. Server processes request and sends `JoinMapInitiated`
3. Server adds player to map and sends `JoinMapCompleted` with map data
4. Server broadcasts `PlayerJoinedMap` to other players on the map

## 4. Game State Management

### Client-Side State

The client maintains a local representation of the game state in the `GameState` class:
- Current map data and player position
- Other players' positions
- Fight state (opponent, cards, HP, etc.)
- Pending operations (movement, etc.)

This state is updated based on messages received from the server.

### Server-Side State

The server maintains the authoritative game state across multiple actors:
- `Player` objects store player data (name, position, current map)
- `Map` objects store map data and player positions
- `FightState` objects store the state of ongoing card battles
- `Deck` and `Card` objects represent the card battle mechanics

## 5. Game Mechanics

### Map Navigation

- Players can join maps using `JoinMapRequest` messages
- Players can move on maps using `PlayerMoveRequest` messages
- The server validates movements and updates all clients

### Card Battle System

#### Battle Initialization
- Players can challenge others to fights
- When a fight starts, a dedicated `FightActor` is created
- Each player starts with 50 HP and a deck of cards

#### Turn-Based Combat
- Players take turns drawing and playing cards
- Each turn, players:
  1. Draw cards (up to hand limit of 10)
  2. Receive action points (3 per turn, unused points carry over)
  3. Play cards by spending action points
  4. End their turn

#### Card Effects
- Cards have various effects (attack, defense, special, utility)
- Effects are processed by specialized handlers on the server
- Effects can deal damage, heal, apply status effects, etc.

#### Status Effects
- Effects can persist across turns (e.g., damage over time)
- Each effect has a type, magnitude, and duration
- Effects are processed at the start of each turn

#### Battle Resolution
- Battle ends when one player's HP reaches 0
- Winner is determined and both players return to map

## 6. Client Implementation Details

The Godot client uses:
- `NetworkManager`: Handles WebSocket communication
- `GameState`: Maintains client-side game state
- Scene-based UI (Title, MapSelect, Game, CardBattle)
- Signal system for event propagation

## 7. Message Processing Pattern

Both client and server use a similar pattern for message processing:

```
Receive Message → Identify Message Type → Dispatch to Handler → Update State → Send Response/Notification
```

This pattern is implemented using:
- Pattern matching on message types
- Dedicated handler methods for each message type
- Event/signal system for notifications

## 8. Key Concepts for LLMs

When reasoning about this system:

1. **Message Flow**: All state changes originate from messages
2. **Actor Isolation**: Actors only communicate through messages
3. **State Synchronization**: Server state is authoritative, client state is a reflection
4. **Event-Driven Architecture**: System behavior is driven by events and messages
5. **Concurrent Processing**: Multiple actors can process messages simultaneously
6. **Domain-Driven Design**: Messages are organized by domain for better maintainability

Understanding these concepts will help you reason about the system's behavior and interactions.
