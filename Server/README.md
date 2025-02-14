# Multiplayer Game Architecture

A real-time multiplayer game system built with .NET Core and Godot, featuring an actor-based server architecture and WebSocket communication.

## Architecture Overview

This project implements a client-server architecture for a multiplayer game with the following key characteristics:

- **Real-time Communication**: WebSocket-based messaging system for instant state updates
- **Actor-Based Concurrency**: Leverages Proto.Actor for scalable, message-driven server architecture
- **Clean Architecture**: Separated into distinct layers with clear responsibilities
- **State Management**: Centralized game state handling with distributed actor system

## System Components

### Server Architecture

The server is organized into three main layers:

1. **Presentation Layer** (`GameServer.Presentation`)
   - WebSocket server implementation
   - Client connection management
   - Message serialization/deserialization
   - External communication protocol

2. **Application Layer** (`GameServer.Application`)
   - Core game logic
   - Actor system implementation
   - Three main actor types:
     - `GameActor`: Central coordinator managing maps and players
     - `MapActor`: Handles individual map state and operations
     - `PlayerActor`: Manages individual player state and actions

3. **Shared Layer** (`GameServer.Shared`)
   - Common models and messages
   - Shared data structures
   - Communication protocols

### Client Implementation

The client is built using the Godot game engine and features:

- **NetworkManager**: Handles WebSocket communication with server
- **State Management**: Local game state synchronization
- **Event System**: Signal-based communication between components
- **Scene Management**: Map selection and game state transitions

## Communication Flow

1. **Connection Establishment**
   - Client connects via WebSocket
   - Server creates a new player actor
   - Connection confirmation sent to client

2. **Game State Management**
   - Map list requests/responses
   - Player join/leave operations
   - Real-time position updates
   - State synchronization messages

3. **Message Types**
   - Internal messages (server-side actor communication)
   - External messages (client-server communication)
   - State update broadcasts

## Technology Stack

- **.NET Core**: Server-side framework
- **Proto.Actor**: Actor system for concurrent operations
- **Godot Engine**: Client-side game engine
- **WebSocket**: Real-time communication protocol
- **JSON**: Message serialization format

## Key Features

### Map System
- Dynamic map creation and management
- Tilemap-based world representation
- Player tracking within maps

### Player Management
- Unique session IDs for players
- Position tracking and updates
- State synchronization

### Real-time Updates
- Immediate position updates
- State broadcast to relevant players
- Failure handling and error responses

### Scalable Architecture
- Actor-based concurrency
- Message-driven communication
- Isolated state management

## Message Flow Example

```
Client -> Server: Join Map Request
Server:
  1. GameSession receives request
  2. Routes to PlayerActor
  3. PlayerActor coordinates with MapActor
  4. State updates propagated
Server -> Client: Join confirmation + map data
```

This architecture provides a solid foundation for real-time multiplayer game development, offering scalability, maintainability, and clear separation of concerns.
