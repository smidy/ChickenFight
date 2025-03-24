#!/usr/bin/env python
"""
Evaluation script for the card battle game.
"""
import os
import sys
import argparse
import logging
import time
from typing import Dict, List, Optional, Any

# Add parent directory to path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from src.utils.config import Config
from src.utils.logger import MetricsLogger
from src.agent import Agent


def parse_args() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(description="Evaluate AI agents for the card battle game")
    
    parser.add_argument(
        "--config", 
        type=str, 
        default="config.json",
        help="Path to configuration file"
    )
    
    parser.add_argument(
        "--checkpoint", 
        type=str, 
        required=True,
        help="Path to checkpoint to load"
    )
    
    parser.add_argument(
        "--num-episodes", 
        type=int, 
        default=10,
        help="Number of episodes to evaluate for"
    )
    
    parser.add_argument(
        "--log-dir", 
        type=str, 
        default="logs/eval",
        help="Directory to save logs"
    )
    
    parser.add_argument(
        "--render", 
        action="store_true",
        help="Render the environment during evaluation"
    )
    
    parser.add_argument(
        "--verbose", 
        action="store_true",
        help="Enable verbose logging"
    )
    
    return parser.parse_args()


def main() -> None:
    """Main function."""
    # Parse arguments
    args = parse_args()
    
    # Set up logging
    log_level = logging.DEBUG if args.verbose else logging.INFO
    logging.basicConfig(
        level=log_level,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )
    logger = logging.getLogger("evaluate")
    
    # Load configuration
    try:
        if os.path.exists(args.config):
            config = Config.from_json(args.config)
            logger.info(f"Loaded configuration from {args.config}")
        else:
            config = Config()
            logger.warning(f"Configuration file {args.config} not found, using default configuration")
    except Exception as e:
        logger.error(f"Error loading configuration: {e}")
        config = Config()
        logger.warning("Using default configuration")
    
    # Create metrics logger
    metrics_logger = MetricsLogger(log_dir=args.log_dir)
    
    # Create agent
    agent = Agent(config, agent_id="eval_agent", logger=metrics_logger)
    
    # Load checkpoint
    if not os.path.exists(args.checkpoint):
        logger.error(f"Checkpoint {args.checkpoint} not found")
        return
    
    try:
        agent.load_checkpoint(args.checkpoint)
        logger.info(f"Loaded checkpoint from {args.checkpoint}")
    except Exception as e:
        logger.error(f"Error loading checkpoint: {e}")
        return
    
    # Connect to server
    if not agent.connect():
        logger.error("Failed to connect to server")
        return
    
    # Join a map
    if not agent.join_map():
        logger.error("Failed to join a map")
        agent.disconnect()
        return
    
    # Evaluate
    try:
        logger.info(f"Starting evaluation for {args.num_episodes} episodes")
        metrics = agent.evaluate(args.num_episodes)
        
        # Log metrics
        logger.info(
            f"Evaluation completed: "
            f"mean_reward={metrics.get('mean_reward', 0.0):.2f}, "
            f"mean_length={metrics.get('mean_length', 0.0):.2f}, "
            f"win_rate={metrics.get('win_rate', 0.0):.2f}"
        )
        
        # Save metrics
        metrics_logger.log_metrics(0, metrics)
        metrics_logger.save_metrics_csv("eval_metrics.csv")
        
        # Plot metrics
        metrics_logger.plot_all_metrics(save=True, show=False)
        
    except Exception as e:
        logger.error(f"Error during evaluation: {e}")
    finally:
        # Disconnect from server
        agent.disconnect()
        logger.info("Disconnected from server")
    
    logger.info("Evaluation completed")


if __name__ == "__main__":
    main()
