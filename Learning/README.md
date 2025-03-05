# Card Battle Game Documentation for LLMs

This documentation set provides a comprehensive overview of the card battle game's architecture, mechanics, and implementation details. It is specifically structured to help LLMs efficiently understand and reason about the system.

## Documentation Index

1. **[Game Architecture Overview](GameArchitecture.txt)**
   - System architecture and components
   - Actor model implementation
   - Client-server communication
   - Game state management
   - Game mechanics overview

2. **[Message System](MessageSystem.txt)**
   - Message architecture and type hierarchy
   - Message flow patterns
   - Message processing pipeline
   - Key message types and their purpose
   - State synchronization
   - Error handling and optimizations

3. **[Card Battle System](CardBattleSystem.txt)**
   - Card battle mechanics
   - Card structure and player state
   - Battle flow and turn sequence
   - Card effect system
   - Technical implementation
   - Message flow for card battles
   - Edge cases and special considerations

4. **[Actor Model Implementation](ActorModelImplementation.txt)**
   - Actor model fundamentals
   - Actor hierarchy and responsibilities
   - Message passing patterns
   - Actor lifecycle and message processing
   - State management
   - Error handling and supervision
   - WebSocket integration
   - Advantages of the actor model

## How to Use This Documentation

When reasoning about this system, consider these key aspects:

1. **Message-Driven Architecture**: All state changes and interactions are driven by messages passing between components.

2. **Actor Isolation**: Each actor encapsulates its own state and only communicates through messages, providing natural concurrency and fault tolerance.

3. **Client-Server Synchronization**: The server maintains the authoritative state, while clients maintain a local representation that is updated based on server messages.

4. **Event-Driven Flow**: The system operates on an event-driven model where actions trigger reactions through a chain of messages.

5. **State Transitions**: Understanding how the system moves between states (e.g., joining a map, starting a fight, playing a card) is crucial for reasoning about its behavior.

## System Diagram

```
┌─────────────────┐                  ┌─────────────────────────────────────┐
│                 │                  │                                     │
│  Godot Client   │◄─WebSocket/JSON─►│  C# Server (Actor Model)            │
│                 │                  │                                     │
└─────────────────┘                  └─────────────────────────────────────┘
       │                                              │
       │                                              │
       ▼                                              ▼
┌─────────────────┐                  ┌─────────────────────────────────────┐
│                 │                  │                                     │
│  NetworkManager │                  │  WebSocketServer                    │
│                 │                  │                                     │
└────────┬────────┘                  └────────────────┬────────────────────┘
         │                                            │
         │                                            │
         ▼                                            ▼
┌─────────────────┐                  ┌─────────────────────────────────────┐
│                 │                  │                                     │
│   GameState     │                  │  Actor System                       │
│                 │                  │  (GameActor, PlayerActor, etc.)     │
│                 │                  │                                     │
└─────────────────┘                  └─────────────────────────────────────┘
```

## Key Concepts for Efficient Understanding

1. **Message Types**: Understanding the different message types and their flow is crucial for reasoning about the system.

2. **State Management**: Each component maintains its own state, which is updated based on messages.

3. **Validation Chain**: Client requests → Server validation → Success/failure response.

4. **Concurrency Model**: Multiple actors process messages concurrently, but each actor processes messages sequentially.

5. **Error Handling**: The system uses explicit error messages and actor isolation to handle failures gracefully.

By focusing on these aspects, an LLM can efficiently understand and reason about the system's behavior in various scenarios.
