# Actor Model Implementation in the Card Battle Game

## 1. Actor Model Fundamentals

The server uses the Actor Model pattern via the Proto.Actor framework to structure its components. This pattern provides:

- **Isolation**: Actors encapsulate state and behavior
- **Message-Passing**: Actors communicate exclusively through messages
- **Concurrency**: Actors can process messages concurrently
- **Fault Tolerance**: Actors can monitor and restart other actors

## 2. Actor Hierarchy

The game server implements a hierarchical actor system:

```
                  ┌─────────────┐
                  │  GameActor  │
                  └──────┬──────┘
                         │
                         ▼
              ┌────────────────────┐
              │   MapManagerActor  │
              └──────────┬─────────┘
                         │
                 ┌───────┴───────┐
                 ▼               ▼
         ┌─────────────┐  ┌─────────────┐
         │  MapActor1  │  │  MapActor2  │ ...
         └──────┬──────┘  └─────────────┘
                │
        ┌───────┴───────┐
        ▼               ▼
┌─────────────┐  ┌─────────────┐
│ PlayerActor1│  │ PlayerActor2│ ...
└─────────────┘  └─────────────┘
        │               │
        └───────┬───────┘
                ▼
        ┌─────────────┐
        │  FightActor │
        └─────────────┘
```

## 3. Actor Responsibilities

### GameActor
- Entry point for the actor system
- Creates and manages the MapManagerActor
- Creates PlayerActors when clients connect

### MapManagerActor
- Manages all maps in the game
- Creates and tracks MapActors
- Handles map listing requests

### MapActor
- Represents a single game map
- Manages player positions and movement
- Validates player actions on the map
- Facilitates player interactions (e.g., fight challenges)

### PlayerActor
- Represents a connected client
- Processes client messages
- Maintains player state
- Communicates with MapActors and FightActors
- Forwards messages to the client

### FightActor
- Created temporarily for card battles
- Manages the state of a fight between two players
- Processes card plays and turn changes
- Applies card effects and updates player states
- Determines battle outcome

## 4. Message Passing Patterns

### Direct Communication

```
PlayerActor ──(PlayCard)──> FightActor
```

### Request-Response

```
PlayerActor ──(RequestMapList)──> MapManagerActor
             <──(MapListResponse)──
```

### Broadcast

```
MapActor ──(PlayerPositionChange)──> PlayerActor1
        └──(PlayerPositionChange)──> PlayerActor2
        └──(PlayerPositionChange)──> PlayerActor3
```

### Mediated Communication

```
PlayerActor1 ──(FightChallengeRequest)──> MapActor ──(FightChallengeReceived)──> PlayerActor2
```

## 5. Actor Lifecycle

### Creation
- GameActor is created at server startup
- MapManagerActor is created by GameActor
- MapActors are created by MapManagerActor
- PlayerActors are created when clients connect
- FightActors are created when fights start

### Termination
- PlayerActors are terminated when clients disconnect
- FightActors are terminated when fights end
- MapActors and MapManagerActor persist for the server lifetime

## 6. Message Processing

Each actor implements the `ReceiveAsync` method to process messages:

```csharp
public Task ReceiveAsync(IContext context)
{
    return context.Message switch
    {
        Started => OnStarted(context),
        CreatePlayer msg => OnCreatePlayer(context, msg),
        // Other message handlers
        _ => Task.CompletedTask
    };
}
```

This pattern matching approach:
- Identifies the message type
- Dispatches to the appropriate handler method
- Updates actor state based on the message
- May send messages to other actors

## 7. State Management

Actors encapsulate and manage their own state:

- **GameActor**: Minimal state (reference to MapManagerActor)
- **MapManagerActor**: Collection of available maps
- **MapActor**: Map data, player positions, active fights
- **PlayerActor**: Player data, current map, current fight
- **FightActor**: Complete fight state (cards, HP, AP, effects)

State is only modified in response to messages, ensuring consistency.

## 8. Error Handling and Supervision

The actor model provides natural error isolation:

- Errors in one actor don't affect others directly
- Actors can monitor child actors and restart them if needed
- Messages can be retried or redirected on failure

## 9. WebSocket Integration

The WebSocket server integrates with the actor system:

1. Client connects via WebSocket
2. Server creates a GameSession for the connection
3. GameSession requests creation of a PlayerActor
4. Client messages are forwarded to the PlayerActor
5. PlayerActor responses are sent back via WebSocket

## 10. Advantages of the Actor Model for This Game

### Scalability
- Actors can be distributed across multiple servers
- New maps and players can be added dynamically
- Processing is naturally parallelized

### Fault Isolation
- Errors in one fight don't affect other fights
- Player disconnections are handled gracefully
- Map issues don't crash the entire server

### Modularity
- Each actor has a clear, single responsibility
- New features can be added by creating new actor types
- Actors can be modified independently

### Concurrency
- Multiple fights can proceed simultaneously
- Player movements are processed concurrently
- No need for explicit locks or synchronization

## 11. Reasoning About the Actor System for LLMs

When analyzing this system, consider:

1. **Message Flow**: Follow the path of messages between actors
2. **State Boundaries**: Each actor owns and manages its state
3. **Concurrency**: Multiple actors process messages simultaneously
4. **Hierarchy**: Parent actors create and supervise child actors
5. **Isolation**: Failures in one actor don't directly affect others

Understanding these principles will help you reason about the system's behavior, especially in complex scenarios involving multiple players and concurrent actions.
