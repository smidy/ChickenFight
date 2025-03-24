#!/usr/bin/env python
"""
Training script for the card battle game.
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
from src.training.trainer import Trainer
from src.training.parallel import ParallelTrainer


def parse_args() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(description="Train AI agents for the card battle game")
    
    parser.add_argument(
        "--config", 
        type=str, 
        default="config.json",
        help="Path to configuration file"
    )
    
    parser.add_argument(
        "--num-episodes", 
        type=int, 
        default=None,
        help="Number of episodes to train for (default: unlimited)"
    )
    
    parser.add_argument(
        "--parallel", 
        action="store_true",
        help="Use parallel training with multiprocessing"
    )
    
    parser.add_argument(
        "--log-dir", 
        type=str, 
        default="logs",
        help="Directory to save logs"
    )
    
    parser.add_argument(
        "--checkpoint-dir", 
        type=str, 
        default=None,
        help="Directory to save checkpoints (overrides config)"
    )
    
    parser.add_argument(
        "--load-checkpoint", 
        type=str, 
        default=None,
        help="Path to checkpoint to load"
    )
    
    parser.add_argument(
        "--eval-interval", 
        type=int, 
        default=None,
        help="Interval (in episodes) to evaluate the agent (overrides config)"
    )
    
    parser.add_argument(
        "--save-interval", 
        type=int, 
        default=None,
        help="Interval (in episodes) to save checkpoints (overrides config)"
    )
    
    parser.add_argument(
        "--num-agents", 
        type=int, 
        default=None,
        help="Number of agents to train (overrides config)"
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
    logger = logging.getLogger("train")
    
    # Load configuration
    try:
        if os.path.exists(args.config):
            config = Config.from_json(args.config)
            logger.info(f"Loaded configuration from {args.config}")
        else:
            config = Config()
            logger.warning(f"Configuration file {args.config} not found, using default configuration")
            
            # Save default configuration
            os.makedirs(os.path.dirname(args.config), exist_ok=True)
            config.save(args.config)
            logger.info(f"Saved default configuration to {args.config}")
    except Exception as e:
        logger.error(f"Error loading configuration: {e}")
        config = Config()
        logger.warning("Using default configuration")
    
    # Override configuration with command line arguments
    if args.checkpoint_dir is not None:
        config.training.checkpoint_dir = args.checkpoint_dir
        os.makedirs(config.training.checkpoint_dir, exist_ok=True)
    
    if args.eval_interval is not None:
        config.training.eval_interval = args.eval_interval
    
    if args.save_interval is not None:
        config.training.save_interval = args.save_interval
    
    if args.num_agents is not None:
        config.training.num_agents = args.num_agents
    
    # Create metrics logger
    metrics_logger = MetricsLogger(log_dir=args.log_dir)
    
    # Create trainer
    if args.parallel:
        trainer = ParallelTrainer(config, logger=metrics_logger)
        logger.info("Using parallel trainer with multiprocessing")
    else:
        trainer = Trainer(config, logger=metrics_logger)
        logger.info("Using sequential trainer")
    
    # Load checkpoint if specified
    if args.load_checkpoint is not None:
        if os.path.exists(args.load_checkpoint):
            logger.info(f"Loading checkpoint from {args.load_checkpoint}")
            # TODO: Implement loading checkpoints for multiple agents
        else:
            logger.error(f"Checkpoint {args.load_checkpoint} not found")
    
    # Start training
    try:
        logger.info("Starting training")
        if args.parallel:
            trainer.start_training()
        else:
            trainer.start_training(args.num_episodes)
        
        # Wait for training to complete or for user to interrupt
        try:
            while True:
                time.sleep(1.0)
        except KeyboardInterrupt:
            logger.info("Interrupted by user")
        finally:
            # Stop training
            if args.parallel:
                trainer.stop_training()
            else:
                trainer.stop_training()
            
            logger.info("Training stopped")
    except Exception as e:
        logger.error(f"Error during training: {e}")
    
    # Save final checkpoints
    try:
        if args.parallel:
            logger.info("Saving final checkpoints")
            # TODO: Implement saving checkpoints for parallel trainer
        else:
            logger.info("Saving final checkpoints")
            checkpoint_paths = trainer.save_checkpoints()
            logger.info(f"Saved checkpoints to {checkpoint_paths}")
    except Exception as e:
        logger.error(f"Error saving checkpoints: {e}")
    
    logger.info("Training completed")


if __name__ == "__main__":
    main()
