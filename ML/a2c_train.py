#!/usr/bin/env python3
"""
A2C Training Script for Card Battle Game

This script implements the A2C (Advantage Actor-Critic) algorithm
to train agents that can play the card battle portion of the game.
Agents are trained in parallel against each other for efficiency.
"""

import asyncio
import argparse
import logging
import os
import sys
import torch
from typing import Dict, List, Optional

from agent_manager import AgentManager

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("a2c_training.log"),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

async def main():
    """Main entry point for A2C training"""
    # Parse command line arguments
    parser = argparse.ArgumentParser(description='A2C training for card battle game')
    parser.add_argument('--server-url', type=str, default='ws://127.0.0.1:8080',
                        help='WebSocket URL of the game server')
    parser.add_argument('--num-agents', type=int, default=4,
                        help='Number of agents to train in parallel')
    parser.add_argument('--num-episodes', type=int, default=1000,
                        help='Number of episodes to train for')
    parser.add_argument('--save-dir', type=str, default='ML/checkpoints',
                        help='Directory to save model checkpoints')
    parser.add_argument('--save-interval', type=int, default=50,
                        help='How often to save model checkpoints (in episodes)')
    parser.add_argument('--cuda', action='store_true',
                        help='Use CUDA if available')
    
    args = parser.parse_args()
    
    # Set device
    if args.cuda and torch.cuda.is_available():
        device = 'cuda'
        logger.info(f"Using CUDA device: {torch.cuda.get_device_name(0)}")
    else:
        device = 'cpu'
        logger.info("Using CPU for training")
    
    try:
        # Create agent manager
        agent_manager = AgentManager(
            server_url=args.server_url,
            num_agents=args.num_agents,
            save_dir=args.save_dir,
            save_interval=args.save_interval,
            device=device
        )
        
        # Initialize agents
        logger.info("Initializing agents...")
        await agent_manager.initialize_agents()
        
        # Join maps
        logger.info("Joining maps...")
        await agent_manager.join_maps()
        
        # Start training
        logger.info(f"Starting training with {args.num_agents} agents for {args.num_episodes} episodes...")
        await agent_manager.train_agents(num_episodes=args.num_episodes)
        
    except KeyboardInterrupt:
        logger.info("Training interrupted by user")
    except Exception as e:
        logger.error(f"Error during training: {e}", exc_info=True)
    finally:
        # Clean up
        if 'agent_manager' in locals():
            await agent_manager.close()
        
        logger.info("Training complete")

if __name__ == "__main__":
    # Run the async main function
    asyncio.run(main())
