# A2C Training for Card Battle Game

This project implements the Advantage Actor-Critic (A2C) reinforcement learning algorithm to train agents to play the card battle portion of the Chicken Fight game. The implementation uses PyTorch with CUDA support for efficient training.

## Features

- Enhanced `GameContext` with RL-specific methods for state observation, action selection, and reward calculation
- PyTorch-based A2C implementation with CUDA support
- Parallel training of multiple agents against each other
- Comprehensive logging and visualization tools
- Automatic agent pairing and battle coordination

## Project Structure

```
ML/
├── a2c_train.py           # Main training script
├── agent_manager.py       # Manages multiple agents and coordination
├── GameContext.py         # Enhanced GameContext with RL capabilities
├── requirements.txt       # Project dependencies
├── checkpoints/           # Saved model checkpoints
├── logs/                  # Training logs and visualizations
├── models/
│   └── a2c_model.py       # A2C neural network architecture
├── agents/
│   └── a2c_agent.py       # A2C agent implementation
└── utils/
    ├── logger.py          # Training logger and visualization
    └── preprocessing.py   # State processing utilities
```

## Setup and Installation

1. Install dependencies:

```bash
pip install -r ML/requirements.txt
```

2. Ensure the game server is running at the default address (`ws://127.0.0.1:8080`) or specify a custom address with the `--server-url` parameter.

## Usage

### Training

To start training with default parameters:

```bash
python ML/a2c_train.py
```

### Advanced Options

```bash
python ML/a2c_train.py --server-url ws://127.0.0.1:8080 --num-agents 4 --num-episodes 1000 --save-dir ML/checkpoints --save-interval 50 --cuda
```

Parameters:
- `--server-url`: WebSocket URL of the game server
- `--num-agents`: Number of agents to train in parallel
- `--num-episodes`: Number of episodes to train for
- `--save-dir`: Directory to save model checkpoints
- `--save-interval`: How often to save model checkpoints (in episodes)
- `--cuda`: Use CUDA if available

## Implementation Details

### A2C Algorithm

The Advantage Actor-Critic algorithm combines:
- Policy network (Actor): Decides which actions to take
- Value network (Critic): Estimates the expected return from each state

The algorithm works by computing the advantage function, which measures how much better taking a specific action is compared to the average action in that state.

### State Representation

Game states are represented as normalized tensors including:
- Player and opponent hit points, action points, deck counts
- Card information (type, cost, effects)
- Hand sizes and card encodings
- Status effects

### Action Space

Actions include:
- Playing specific cards from hand
- Ending the current turn
- Challenging other players (outside of battle)

### Reward Structure

The reward system includes:
- Major reward (+1.0) for winning battles
- Major penalty (-1.0) for losing battles
- Intermediate rewards for dealing damage
- Penalties for taking damage
- Small rewards for efficient card usage

## Training Process

1. Agents connect to the server and obtain player IDs
2. Agents join maps and pair up for battles
3. During battles, agents collect experiences and learn from them
4. After each episode, networks are updated based on collected experiences
5. Training statistics are logged and visualized

## Visualization and Logging

Training progress can be monitored through:
- Real-time console logging
- Reward, win rate, and loss plots
- Comprehensive HTML reports
- Detailed JSON statistics

## License

This project is part of the Chicken Fight game solution and follows the same licensing.
