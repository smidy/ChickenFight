"""
Logging utilities for A2C training.

This module provides functions for logging training progress,
visualizing results, and tracking statistics.
"""

import logging
import time
import json
import os
import matplotlib.pyplot as plt
import numpy as np
from datetime import datetime
from typing import Dict, List, Optional, Any, Union

# Configure logger
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("a2c_training.log"),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

class TrainingLogger:
    """
    Logger for A2C training statistics and visualization.
    """
    
    def __init__(self, log_dir: str = 'ML/logs'):
        """
        Initialize the training logger.
        
        Args:
            log_dir: Directory to save log files and visualizations
        """
        self.log_dir = log_dir
        os.makedirs(log_dir, exist_ok=True)
        
        # Initialize statistics tracking
        self.episode_rewards = []
        self.win_rates = []
        self.policy_losses = []
        self.value_losses = []
        self.total_losses = []
        self.timestamps = []
        
        # Initialize log file
        self.log_file = os.path.join(log_dir, f"training_log_{datetime.now().strftime('%Y%m%d_%H%M%S')}.jsonl")
        
        logger.info(f"Training logger initialized. Logs will be saved to {self.log_file}")
    
    def log_episode(self, 
                    episode: int, 
                    reward: float, 
                    win: bool, 
                    stats: Dict[str, Any]):
        """
        Log statistics for a completed episode.
        
        Args:
            episode: Episode number
            reward: Total episode reward
            win: Whether the episode resulted in a win
            stats: Dictionary of training statistics
        """
        # Save statistics
        self.episode_rewards.append(reward)
        self.win_rates.append(stats.get('win_rate', 0.0))
        self.policy_losses.append(stats.get('policy_loss', 0.0))
        self.value_losses.append(stats.get('value_loss', 0.0))
        self.total_losses.append(stats.get('total_loss', 0.0))
        self.timestamps.append(time.time())
        
        # Create log entry
        log_entry = {
            'episode': episode,
            'timestamp': time.time(),
            'reward': reward,
            'win': win,
            'stats': stats
        }
        
        # Write to log file
        with open(self.log_file, 'a') as f:
            f.write(json.dumps(log_entry) + '\n')
        
        # Log to console
        logger.info(
            f"Episode {episode}: "
            f"Reward = {reward:.2f}, "
            f"Win = {win}, "
            f"Win Rate = {stats.get('win_rate', 0.0):.2f}, "
            f"Policy Loss = {stats.get('policy_loss', 0.0):.4f}, "
            f"Value Loss = {stats.get('value_loss', 0.0):.4f}"
        )
    
    def log_training_step(self, 
                         step: int, 
                         stats: Dict[str, Any]):
        """
        Log statistics for a training step.
        
        Args:
            step: Training step number
            stats: Dictionary of training statistics
        """
        # Save statistics
        self.policy_losses.append(stats.get('policy_loss', 0.0))
        self.value_losses.append(stats.get('value_loss', 0.0))
        self.total_losses.append(stats.get('total_loss', 0.0))
        self.timestamps.append(time.time())
        
        # Create log entry
        log_entry = {
            'step': step,
            'timestamp': time.time(),
            'stats': stats
        }
        
        # Write to log file
        with open(self.log_file, 'a') as f:
            f.write(json.dumps(log_entry) + '\n')
    
    def plot_rewards(self, save: bool = True, show: bool = False):
        """
        Plot episode rewards over time.
        
        Args:
            save: Whether to save the plot to a file
            show: Whether to display the plot
        
        Returns:
            str: Path to the saved plot file if save=True
        """
        plt.figure(figsize=(10, 6))
        plt.plot(self.episode_rewards)
        plt.title('Episode Rewards')
        plt.xlabel('Episode')
        plt.ylabel('Total Reward')
        plt.grid(True)
        
        if save:
            plot_path = os.path.join(self.log_dir, 'rewards.png')
            plt.savefig(plot_path)
            logger.info(f"Rewards plot saved to {plot_path}")
        
        if show:
            plt.show()
        else:
            plt.close()
            
        return os.path.join(self.log_dir, 'rewards.png') if save else None
    
    def plot_win_rate(self, window: int = 100, save: bool = True, show: bool = False):
        """
        Plot win rate over time.
        
        Args:
            window: Window size for moving average
            save: Whether to save the plot to a file
            show: Whether to display the plot
        
        Returns:
            str: Path to the saved plot file if save=True
        """
        plt.figure(figsize=(10, 6))
        plt.plot(self.win_rates)
        
        # Add moving average if we have enough data
        if len(self.win_rates) >= window:
            moving_avg = np.convolve(self.win_rates, np.ones(window)/window, mode='valid')
            plt.plot(range(window-1, len(self.win_rates)), moving_avg, 'r-', linewidth=2)
            plt.legend(['Win Rate', f'{window}-Episode Moving Average'])
        
        plt.title('Win Rate')
        plt.xlabel('Episode')
        plt.ylabel('Win Rate')
        plt.grid(True)
        plt.ylim(0, 1)
        
        if save:
            plot_path = os.path.join(self.log_dir, 'win_rate.png')
            plt.savefig(plot_path)
            logger.info(f"Win rate plot saved to {plot_path}")
        
        if show:
            plt.show()
        else:
            plt.close()
            
        return os.path.join(self.log_dir, 'win_rate.png') if save else None
    
    def plot_losses(self, save: bool = True, show: bool = False):
        """
        Plot training losses over time.
        
        Args:
            save: Whether to save the plot to a file
            show: Whether to display the plot
        
        Returns:
            str: Path to the saved plot file if save=True
        """
        plt.figure(figsize=(10, 6))
        plt.plot(self.policy_losses, label='Policy Loss')
        plt.plot(self.value_losses, label='Value Loss')
        plt.plot(self.total_losses, label='Total Loss')
        plt.title('Training Losses')
        plt.xlabel('Training Step')
        plt.ylabel('Loss')
        plt.legend()
        plt.grid(True)
        
        if save:
            plot_path = os.path.join(self.log_dir, 'losses.png')
            plt.savefig(plot_path)
            logger.info(f"Losses plot saved to {plot_path}")
        
        if show:
            plt.show()
        else:
            plt.close()
            
        return os.path.join(self.log_dir, 'losses.png') if save else None
    
    def save_statistics(self):
        """
        Save all statistics to a file.
        
        Returns:
            str: Path to the saved statistics file
        """
        stats = {
            'episode_rewards': self.episode_rewards,
            'win_rates': self.win_rates,
            'policy_losses': self.policy_losses,
            'value_losses': self.value_losses,
            'total_losses': self.total_losses,
            'timestamps': self.timestamps
        }
        
        stats_file = os.path.join(self.log_dir, 'training_stats.json')
        with open(stats_file, 'w') as f:
            json.dump(stats, f, indent=2)
            
        logger.info(f"Training statistics saved to {stats_file}")
        return stats_file
    
    def generate_report(self):
        """
        Generate a comprehensive training report with plots and statistics.
        
        Returns:
            str: Path to the generated report
        """
        # Save all plots
        self.plot_rewards(show=False)
        self.plot_win_rate(show=False)
        self.plot_losses(show=False)
        
        # Save statistics
        self.save_statistics()
        
        # Generate HTML report
        report_file = os.path.join(self.log_dir, 'training_report.html')
        
        with open(report_file, 'w') as f:
            f.write(f"""
            <html>
            <head>
                <title>A2C Training Report</title>
                <style>
                    body {{ font-family: Arial, sans-serif; margin: 20px; }}
                    h1, h2 {{ color: #333; }}
                    .container {{ max-width: 1200px; margin: 0 auto; }}
                    .plot {{ margin: 20px 0; text-align: center; }}
                    .plot img {{ max-width: 100%; }}
                    .stats {{ margin: 20px 0; }}
                    table {{ border-collapse: collapse; width: 100%; }}
                    th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                    th {{ background-color: #f2f2f2; }}
                    tr:nth-child(even) {{ background-color: #f9f9f9; }}
                </style>
            </head>
            <body>
                <div class="container">
                    <h1>A2C Training Report</h1>
                    <p>Generated on {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}</p>
                    
                    <div class="stats">
                        <h2>Training Statistics</h2>
                        <table>
                            <tr>
                                <th>Metric</th>
                                <th>Value</th>
                            </tr>
                            <tr>
                                <td>Total Episodes</td>
                                <td>{len(self.episode_rewards)}</td>
                            </tr>
                            <tr>
                                <td>Average Reward</td>
                                <td>{np.mean(self.episode_rewards):.2f}</td>
                            </tr>
                            <tr>
                                <td>Final Win Rate</td>
                                <td>{self.win_rates[-1] if self.win_rates else 0:.2f}</td>
                            </tr>
                            <tr>
                                <td>Total Training Steps</td>
                                <td>{len(self.policy_losses)}</td>
                            </tr>
                        </table>
                    </div>
                    
                    <div class="plot">
                        <h2>Episode Rewards</h2>
                        <img src="rewards.png" alt="Episode Rewards">
                    </div>
                    
                    <div class="plot">
                        <h2>Win Rate</h2>
                        <img src="win_rate.png" alt="Win Rate">
                    </div>
                    
                    <div class="plot">
                        <h2>Training Losses</h2>
                        <img src="losses.png" alt="Training Losses">
                    </div>
                </div>
            </body>
            </html>
            """)
            
        logger.info(f"Training report generated at {report_file}")
        return report_file
