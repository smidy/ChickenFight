# Card Battle System: Detailed Mechanics for LLMs

## 1. Card Battle Overview

The card battle system is a turn-based combat mechanism where players use cards with various effects to reduce their opponent's hit points to zero. The system combines strategic deck building, resource management, and tactical card play.

## 2. Core Components

### Card Structure

Each card has the following properties:
- **ID**: Unique identifier (e.g., `atk_001`, `def_002`)
- **Name**: Display name
- **Description**: Text describing the card's effect
- **Cost**: Action points required to play the card
- **Type**: Category (Attack, Defense, Special, Utility)
- **Effect**: The actual gameplay effect when played

### Player State During Battle

Each player maintains:
- **Hit Points (HP)**: Initially 50, reduced by damage
- **Action Points (AP)**: Resource for playing cards (3 gained per turn)
- **Hand**: Currently available cards (up to 10)
- **Deck**: Cards available to draw
- **Discard Pile**: Cards that have been played or discarded

### Status Effects

Status effects can be applied to players and persist across turns:
- **Types**: DamageOverTime, HealOverTime, DefenseBoost, etc.
- **Duration**: Number of turns the effect remains active
- **Magnitude**: Numerical value of the effect
- **Source**: The card or effect that applied the status

## 3. Battle Flow

### Initialization

1. `FightActor` is created when two players start a battle
2. Each player is assigned a starting deck of cards
3. Initial state is set (50 HP, 0 AP, empty hand)
4. First player is randomly selected to start

### Turn Sequence

1. **Turn Start**:
   - Process active status effects
   - Grant 3 action points
   - Draw cards (up to hand limit of 10)
   - Send `OutTurnStarted` and `OutFightStateUpdate`

2. **Player Actions**:
   - Player can play any number of cards if they have sufficient AP
   - Each card play:
     - Validates action point cost
     - Applies card effect
     - Updates game state
     - Sends notifications (`OutCardPlayCompleted`, `OutEffectApplied`)
     - Sends updated state (`OutFightStateUpdate`)

3. **Turn End**:
   - Player sends `InEndTurn`
   - Discard remaining cards in hand
   - Send `OutTurnEnded`
   - Start opponent's turn

### Battle Resolution

- Battle ends when a player's HP reaches 0
- Winner is determined and `OutFightEnded` is sent to both players
- Players return to the map

## 4. Card Effect System

### Effect Categories

1. **Attack Effects**:
   - Direct damage to opponent
   - Damage over time effects
   - Armor penetration

2. **Defense Effects**:
   - Damage reduction
   - Shields/barriers
   - Healing

3. **Special Effects**:
   - Status effect application
   - Card manipulation (draw, discard)
   - Action point manipulation

4. **Utility Effects**:
   - Card drawing
   - Resource management
   - Special conditions

### Effect Implementation

The server uses a factory pattern to create appropriate handlers for each card:

```
Card → CardEffectHandlerFactory → Specific Handler → Apply Effect
```

Handlers are registered for different card types and implement the `ICardEffectHandler` interface.

## 5. Technical Implementation

### Server-Side (C#)

The `FightActor` manages the battle state and processes all card-related actions:

- `OnPlayCard`: Handles card play requests
- `ApplyCardEffects`: Applies card effects using the appropriate handler
- `SendFightStateUpdate`: Sends updated state to both players

Card effects are implemented in specialized handler classes:
- `AttackCardEffectHandlers`
- `DefenseCardEffectHandlers`
- `SpecialCardEffectHandlers`
- `UtilityCardEffectHandlers`

### Client-Side (Godot)

The client maintains a local representation of the battle state in `GameState`:
- `PlayerHitPoints`, `OpponentHitPoints`
- `PlayerActionPoints`, `OpponentActionPoints`
- `CardsInHand`, `OpponentCardsInHand`
- `PlayerStatusEffects`, `OpponentStatusEffects`

The `CardBattle.cs` script handles UI interactions and sends card play requests to the server.

## 6. Card Representation

Cards are visually represented using SVG data sent from the server:

1. Server reads SVG templates from text files in the Assets directory
2. SVG data is sent to clients via `OutCardImages` and `OutCardDrawn` messages
3. Client caches SVG data for efficient rendering
4. Cards are rendered in the UI with their name, description, and cost

## 7. Message Flow for Card Battles

### Card Play Sequence

```
Client                                Server
  │                                     │
  │─── InPlayCard(cardId) ────────────>│
  │                                     │ Validate card play
  │                                     │ Apply card effects
  │<── OutCardPlayCompleted ────────────│
  │<── OutEffectApplied ─────────────────│ (multiple if needed)
  │<── OutFightStateUpdate ──────────────│
```

### Turn Change Sequence

```
Client                                Server
  │                                     │
  │─── InEndTurn ─────────────────────>│
  │                                     │ Process turn end
  │<── OutTurnEnded ────────────────────│
  │                                     │ Process turn start
  │<── OutTurnStarted ───────────────────│ (for opponent)
  │<── OutCardDrawn ────────────────────│ (multiple)
  │<── OutFightStateUpdate ──────────────│
```

## 8. Edge Cases and Special Considerations

### Card Availability

- If a player's deck is empty, they cannot draw more cards
- Players can still play cards in hand even with an empty deck
- Battle continues until a player's HP reaches 0

### Action Point Management

- Unused action points carry over to the next turn
- Some cards can manipulate action points (grant or reduce)
- Maximum action points is not explicitly limited

### Status Effect Interactions

- Multiple status effects can be active simultaneously
- Effects of the same type stack in duration but not in magnitude
- Some effects can cancel or modify other effects

### Disconnection Handling

- If a player disconnects during battle, the opponent automatically wins
- The `FightActor` handles player disconnection and ends the fight gracefully

## 9. Reasoning About Card Battles for LLMs

When analyzing card battles in this system, consider:

1. **Resource Management**: Balance between spending and saving action points
2. **Card Synergies**: How cards interact with each other and status effects
3. **State Transitions**: How the battle state evolves after each action
4. **Information Asymmetry**: Players know their own deck but not their opponent's
5. **Probabilistic Elements**: Card drawing introduces randomness

Understanding these dynamics will help you reason about strategic decisions and game balance.
