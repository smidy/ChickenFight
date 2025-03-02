This is a multiplayer game with a client/server architecture that features a card battle system. The game combines exploration on a 2D map with turn-based card battles between players. Let me break down the key components and mechanics.

Architecture Overview
Server-Side Components
Actor System: The server uses an actor-based architecture with Proto.Actor framework, where different components of the game are represented as actors that communicate via messages.

Key Server Actors:

GameActor: The main entry point for the game server
PlayerActor: Represents a connected player and handles their actions
MapActor: Manages a game map, player positions, and interactions
FightActor: Manages a card battle between two players
MapManagerActor: Coordinates multiple maps
WebSocket Communication: The server uses WebSockets for real-time communication with clients, serializing messages as JSON.

Client-Side Components
Godot Engine: The client is built using the Godot game engine with C# scripts.

Key Client Scripts:

NetworkManager: Handles WebSocket communication with the server
GameState: Maintains the client-side game state
Game: Main game scene that handles player movement and map display
CardBattle: UI for the card battle system
Game Flow
Connection: Players connect to the server via WebSocket.
Map Selection: Players can join different maps.
Exploration: Players move around on a 2D grid-based map.
Initiating Battles: Players can challenge others to card battles by right-clicking on them.
Card Battle: When a battle starts, the map view is replaced with the card battle UI.
Battle Resolution: After a battle ends, players return to the map view.
Card Battle Mechanics
Core Battle System
Starting Conditions:

Players start with 50 HP
Players receive 3 action points per turn
Players draw 5 cards per turn (up to a maximum hand size of 10)
Turn Structure:

Draw cards at the start of turn
Play cards by spending action points
End turn (unused action points carry over)
Discard hand at end of turn
Victory Condition: Reduce opponent's HP to 0

Card System
Card Types:

Attack: Deal damage to opponents
Defense: Reduce damage, heal, or provide shields
Utility: Draw cards, gain energy, discard opponent's cards, etc.
Special: Powerful effects with high costs
Card Subtypes:

Attack: DirectDamage, AreaOfEffect, Piercing, Vampiric, Combo
Defense: Shield, Redirect, Heal, Dodge, Fortify
Utility: Draw, EnergyBoost, Discard, Lock, Transform
Special: Ultimate, Environment, Summon, Curse, Fusion
Card Properties:

ID: Unique identifier
Name: Display name
Type: Main category
Subtype: Specific effect category
Cost: Action points required to play
Description: Text description of effect
Deck Management:

Players have a deck of up to 25 cards
Cards are drawn from the deck into the hand
When the deck is empty, the discard pile is shuffled back in
Cards played or discarded go to the discard pile
Fight Mechanics in Detail
Initiating a Fight:

Player challenges another player
Target player accepts (auto-accept in current implementation)
Server creates a FightActor to manage the battle
Card Play Process:

Player selects a card from their hand
Server validates if the player has enough action points
Card effect is applied (damage, healing, etc.)
State is updated and broadcast to both players
Card Effects:

Attack cards: Deal damage based on cost (cost × 2)
Defense cards: Heal based on cost (cost × 2)
More complex effects are defined in the card descriptions
Visual Representation:

Cards are displayed as SVG images
Card data is stored in text files on the server
Effects are animated on the client
State Synchronization:

Server maintains the authoritative state
Updates are sent to clients after each action
Clients update their local state based on server messages
Technical Implementation Details
Message-Based Communication:

Internal messages between actors
External messages between server and clients
State Management:

Server maintains the authoritative state
Clients maintain a local representation of the state
State is synchronized via messages
Card Rendering:

SVG data for cards is cached on the server
SVG data is sent to clients for rendering
Cards are displayed with tooltips showing details
Error Handling:

Server validates all actions
Clients display error messages when actions fail
Connection loss is handled gracefully
This client/server game solution combines real-time exploration with strategic turn-based card battles, using a robust actor-based architecture for the server and a responsive Godot-based client.