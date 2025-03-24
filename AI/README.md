# Card Battle AI Training Solution

This project implements an AI training solution for the card battle segment of the game using PyTorch with CUDA support and the Advantage Actor-Critic (A2C) reinforcement learning algorithm.

## Overview

The AI agents connect to the game server via WebSocket, navigate the map, find players who are not in a fight, challenge them to a fight, and then train on the card battle gameplay.

## Features

- WebSocket communication with the game server
- Reinforcement learning environment for the card battle game
- A2C model implementation with PyTorch and CUDA support
- Parallel training with multiple agents
- Comprehensive metrics tracking

## Requirements

- Python 3.8+
- PyTorch 2.6.0 with CUDA 11.8
- Other dependencies listed in requirements.txt

## Project Structure

```
AI/
├── requirements.txt
├── README.md
├── src/
│   ├── __init__.py
│   ├── agent.py              # Agent implementation
│   ├── environment.py        # RL environment wrapper
│   ├── models/
│   │   ├── __init__.py
│   │   ├── a2c.py            # A2C model implementation
│   │   └── network.py        # Neural network architecture
│   ├── communication/
│   │   ├── __init__.py
│   │   ├── websocket_client.py  # WebSocket client
│   │   └── message_handler.py   # Message serialization/deserialization
│   ├── training/
│   │   ├── __init__.py
│   │   ├── trainer.py        # Training orchestration
│   │   └── parallel.py       # Parallel training utilities
│   └── utils/
│       ├── __init__.py
│       ├── logger.py         # Logging utilities
│       └── config.py         # Configuration management
└── scripts/
    ├── train.py              # Training script
    └── evaluate.py           # Evaluation script
```

## Setup

1. Install dependencies:
   ```
   pip install -r requirements.txt
   ```

2. Ensure the game server is running

3. Run the training script:
   ```
   python scripts/train.py
   ```

## Training Approach

The training uses a hybrid approach:
1. Initial random exploration to gather diverse experiences
2. Rule-based guidance with simple heuristics
3. Progressive learning that gradually shifts from rule-based to learned policy

## Metrics

The training tracks the following metrics:
- Win rate
- Average reward
- Episode length
- Action distribution
- HP differential
- Model loss
- Policy entropy
- Value prediction error
