"""
Logging utilities for the AI training solution.
"""
import os
import logging
import time
from datetime import datetime
from typing import Dict, Any, Optional, List, Union

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
from matplotlib.figure import Figure


class MetricsLogger:
    """
    Logger for tracking and visualizing training metrics.
    """
    
    def __init__(self, log_dir: str = "logs"):
        """
        Initialize the metrics logger.
        
        Args:
            log_dir: Directory to save logs and visualizations
        """
        self.log_dir = log_dir
        os.makedirs(log_dir, exist_ok=True)
        
        # Initialize metrics storage
        self.metrics: Dict[str, List[float]] = {}
        self.episodes: List[int] = []
        
        # Set up logging
        self.logger = logging.getLogger("MetricsLogger")
        self.logger.setLevel(logging.INFO)
        
        # File handler
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        log_file = os.path.join(log_dir, f"training_{timestamp}.log")
        file_handler = logging.FileHandler(log_file)
        file_handler.setLevel(logging.INFO)
        
        # Console handler
        console_handler = logging.StreamHandler()
        console_handler.setLevel(logging.INFO)
        
        # Formatter
        formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
        file_handler.setFormatter(formatter)
        console_handler.setFormatter(formatter)
        
        # Add handlers
        self.logger.addHandler(file_handler)
        self.logger.addHandler(console_handler)
        
        self.logger.info(f"Metrics logger initialized. Logs will be saved to {log_dir}")
    
    def log_metrics(self, episode: int, metrics: Dict[str, float]) -> None:
        """
        Log metrics for a specific episode.
        
        Args:
            episode: Episode number
            metrics: Dictionary of metric names and values
        """
        self.episodes.append(episode)
        
        for name, value in metrics.items():
            if name not in self.metrics:
                self.metrics[name] = []
            self.metrics[name].append(value)
        
        # Log to console and file
        metrics_str = ", ".join([f"{name}: {value:.4f}" for name, value in metrics.items()])
        self.logger.info(f"Episode {episode}: {metrics_str}")
    
    def save_metrics_csv(self, filename: Optional[str] = None) -> str:
        """
        Save metrics to a CSV file.
        
        Args:
            filename: Optional filename for the CSV file
            
        Returns:
            Path to the saved CSV file
        """
        if filename is None:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"metrics_{timestamp}.csv"
        
        filepath = os.path.join(self.log_dir, filename)
        
        # Create DataFrame
        data = {"episode": self.episodes}
        for name, values in self.metrics.items():
            # Ensure all metrics have the same length
            if len(values) < len(self.episodes):
                values.extend([np.nan] * (len(self.episodes) - len(values)))
            data[name] = values
        
        df = pd.DataFrame(data)
        df.to_csv(filepath, index=False)
        
        self.logger.info(f"Metrics saved to {filepath}")
        return filepath
    
    def plot_metric(self, metric_name: str, title: Optional[str] = None, 
                   save: bool = True, show: bool = False) -> Optional[Figure]:
        """
        Plot a specific metric over episodes.
        
        Args:
            metric_name: Name of the metric to plot
            title: Optional title for the plot
            save: Whether to save the plot to a file
            show: Whether to display the plot
            
        Returns:
            Matplotlib figure if show=True, otherwise None
        """
        if metric_name not in self.metrics:
            self.logger.warning(f"Metric '{metric_name}' not found in logged metrics")
            return None
        
        fig, ax = plt.subplots(figsize=(10, 6))
        ax.plot(self.episodes, self.metrics[metric_name])
        
        if title:
            ax.set_title(title)
        else:
            ax.set_title(f"{metric_name} over Episodes")
        
        ax.set_xlabel("Episode")
        ax.set_ylabel(metric_name)
        ax.grid(True)
        
        if save:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"{metric_name}_{timestamp}.png"
            filepath = os.path.join(self.log_dir, filename)
            plt.savefig(filepath)
            self.logger.info(f"Plot saved to {filepath}")
        
        if show:
            plt.show()
            return fig
        else:
            plt.close(fig)
            return None
    
    def plot_all_metrics(self, save: bool = True, show: bool = False) -> List[Optional[Figure]]:
        """
        Plot all tracked metrics.
        
        Args:
            save: Whether to save the plots to files
            show: Whether to display the plots
            
        Returns:
            List of Matplotlib figures if show=True, otherwise empty list
        """
        figures = []
        for metric_name in self.metrics.keys():
            fig = self.plot_metric(metric_name, save=save, show=show)
            if show and fig is not None:
                figures.append(fig)
        
        return figures
    
    def plot_comparison(self, metric_names: List[str], title: str = "Metrics Comparison",
                       save: bool = True, show: bool = False) -> Optional[Figure]:
        """
        Plot multiple metrics on the same graph for comparison.
        
        Args:
            metric_names: List of metric names to compare
            title: Title for the plot
            save: Whether to save the plot to a file
            show: Whether to display the plot
            
        Returns:
            Matplotlib figure if show=True, otherwise None
        """
        fig, ax = plt.subplots(figsize=(12, 8))
        
        for metric_name in metric_names:
            if metric_name in self.metrics:
                ax.plot(self.episodes, self.metrics[metric_name], label=metric_name)
            else:
                self.logger.warning(f"Metric '{metric_name}' not found in logged metrics")
        
        ax.set_title(title)
        ax.set_xlabel("Episode")
        ax.set_ylabel("Value")
        ax.grid(True)
        ax.legend()
        
        if save:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"comparison_{timestamp}.png"
            filepath = os.path.join(self.log_dir, filename)
            plt.savefig(filepath)
            self.logger.info(f"Comparison plot saved to {filepath}")
        
        if show:
            plt.show()
            return fig
        else:
            plt.close(fig)
            return None


# Create a default logger instance
DEFAULT_LOGGER = MetricsLogger()
