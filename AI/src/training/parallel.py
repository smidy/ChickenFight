"""
Parallel training utilities for the card battle game.
"""
import os
import time
import logging
import threading
import multiprocessing
import numpy as np
from typing import Dict, List, Tuple, Optional, Any, Union, Callable

from ..agent import Agent
from ..utils.config import Config
from ..utils.logger import MetricsLogger


class ParallelTrainer:
    """
    Parallel trainer for the card battle game.
    
    This class manages multiple agents training in parallel using multiprocessing.
    """
    
    def __init__(self, config: Config, logger: Optional[MetricsLogger] = None):
        """
        Initialize the parallel trainer.
        
        Args:
            config: Configuration
            logger: Metrics logger
        """
        self.config = config
        self.logger = logger or MetricsLogger(log_dir="logs/parallel_trainer")
        
        # Set up logging
        self.log = logging.getLogger("ParallelTrainer")
        self.log.setLevel(logging.INFO)
        
        # Training state
        self.is_training = False
        self.processes: List[multiprocessing.Process] = []
        self.stop_event = multiprocessing.Event()
        
        # Create checkpoint directory
        os.makedirs(config.training.checkpoint_dir, exist_ok=True)
        
        # Shared metrics
        self.manager = multiprocessing.Manager()
        self.shared_metrics = self.manager.dict()
        self.shared_metrics["total_episodes"] = 0
        self.shared_metrics["win_rates"] = self.manager.list([0.0] * config.training.num_agents)
        self.shared_metrics["mean_rewards"] = self.manager.list([0.0] * config.training.num_agents)
    
    def _agent_process(self, agent_id: int, stop_event: multiprocessing.Event,
                      shared_metrics: Dict[str, Any]) -> None:
        """
        Process function for an agent.
        
        Args:
            agent_id: Agent ID
            stop_event: Event to signal stopping
            shared_metrics: Shared metrics dictionary
        """
        try:
            # Set up logging
            log = logging.getLogger(f"Agent-{agent_id}")
            log.setLevel(logging.INFO)
            
            # Create agent
            agent_logger = MetricsLogger(log_dir=os.path.join("logs", f"agent_{agent_id}"))
            agent = Agent(self.config, agent_id=f"agent_{agent_id}", logger=agent_logger)
            
            # Connect to server
            if not agent.connect():
                log.error("Failed to connect to server")
                return
            
            # Join a map
            if not agent.join_map():
                log.error("Failed to join a map")
                return
            
            # Training loop
            while not stop_event.is_set():
                # Train for one episode
                metrics = agent.train_episode()
                
                # Update shared metrics
                shared_metrics["total_episodes"] += 1
                shared_metrics["win_rates"][agent_id] = metrics.get("win_rate", 0.0)
                shared_metrics["mean_rewards"][agent_id] = metrics.get("mean_reward", 0.0)
                
                # Save checkpoint if needed
                if (agent.model.episodes_completed % self.config.training.save_interval == 0 and
                    agent.model.episodes_completed > 0):
                    agent.save_checkpoint()
            
            # Disconnect from server
            agent.disconnect()
            
        except Exception as e:
            log.error(f"Error in agent process: {e}")
    
    def start_training(self) -> None:
        """Start training all agents in parallel."""
        if self.is_training:
            self.log.warning("Training is already in progress")
            return
        
        self.is_training = True
        self.stop_event.clear()
        
        # Start processes for each agent
        for i in range(self.config.training.num_agents):
            process = multiprocessing.Process(
                target=self._agent_process,
                args=(i, self.stop_event, self.shared_metrics)
            )
            process.daemon = True
            process.start()
            self.processes.append(process)
        
        # Start monitoring thread
        self.monitoring_thread = threading.Thread(
            target=self._monitoring_loop
        )
        self.monitoring_thread.daemon = True
        self.monitoring_thread.start()
    
    def _monitoring_loop(self) -> None:
        """Monitoring loop for training progress."""
        try:
            while self.is_training:
                # Log training progress
                self._log_training_progress()
                
                # Sleep
                time.sleep(10.0)
        except Exception as e:
            self.log.error(f"Error in monitoring loop: {e}")
    
    def _log_training_progress(self) -> None:
        """Log training progress."""
        # Get metrics from shared dictionary
        total_episodes = self.shared_metrics["total_episodes"]
        win_rates = list(self.shared_metrics["win_rates"])
        mean_rewards = list(self.shared_metrics["mean_rewards"])
        
        # Log metrics
        self.log.info(
            f"Training progress: "
            f"total_episodes={total_episodes}, "
            f"mean_win_rate={np.mean(win_rates):.2f}, "
            f"mean_reward={np.mean(mean_rewards):.2f}"
        )
        
        # Log to metrics logger
        if self.logger:
            self.logger.log_metrics(
                total_episodes,
                {
                    "mean_win_rate": np.mean(win_rates),
                    "mean_reward": np.mean(mean_rewards)
                }
            )
    
    def stop_training(self) -> None:
        """Stop training all agents."""
        if not self.is_training:
            self.log.warning("Training is not in progress")
            return
        
        # Signal all processes to stop
        self.stop_event.set()
        
        # Wait for processes to terminate
        for process in self.processes:
            process.join(timeout=5.0)
            if process.is_alive():
                self.log.warning(f"Process {process.name} did not terminate properly")
                process.terminate()
        
        self.processes = []
        self.is_training = False


class SharedMemoryBuffer:
    """
    Shared memory buffer for parallel training.
    
    This class provides a shared memory buffer for storing experiences
    that can be accessed by multiple processes.
    """
    
    def __init__(self, capacity: int, state_dim: int, action_dim: int):
        """
        Initialize the shared memory buffer.
        
        Args:
            capacity: Maximum capacity of the buffer
            state_dim: Dimension of the state
            action_dim: Dimension of the action space
        """
        self.capacity = capacity
        self.state_dim = state_dim
        self.action_dim = action_dim
        
        # Create shared memory arrays
        self.manager = multiprocessing.Manager()
        self.states = self.manager.list([np.zeros(state_dim, dtype=np.float32) for _ in range(capacity)])
        self.actions = self.manager.list([0 for _ in range(capacity)])
        self.rewards = self.manager.list([0.0 for _ in range(capacity)])
        self.next_states = self.manager.list([np.zeros(state_dim, dtype=np.float32) for _ in range(capacity)])
        self.dones = self.manager.list([False for _ in range(capacity)])
        
        # Shared counter
        self.counter = multiprocessing.Value('i', 0)
        self.lock = multiprocessing.Lock()
    
    def add(self, state: np.ndarray, action: int, reward: float, 
           next_state: np.ndarray, done: bool) -> None:
        """
        Add a transition to the buffer.
        
        Args:
            state: Current state
            action: Action taken
            reward: Reward received
            next_state: Next state
            done: Whether the episode is done
        """
        with self.lock:
            idx = self.counter.value % self.capacity
            self.states[idx] = state
            self.actions[idx] = action
            self.rewards[idx] = reward
            self.next_states[idx] = next_state
            self.dones[idx] = done
            self.counter.value += 1
    
    def sample(self, batch_size: int) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
        """
        Sample a batch of transitions from the buffer.
        
        Args:
            batch_size: Number of transitions to sample
            
        Returns:
            Batch of transitions (states, actions, rewards, next_states, dones)
        """
        with self.lock:
            size = min(self.counter.value, self.capacity)
            if size == 0:
                return (
                    np.zeros((0, self.state_dim), dtype=np.float32),
                    np.zeros(0, dtype=np.int64),
                    np.zeros(0, dtype=np.float32),
                    np.zeros((0, self.state_dim), dtype=np.float32),
                    np.zeros(0, dtype=np.bool_)
                )
            
            indices = np.random.choice(size, min(batch_size, size), replace=False)
            
            return (
                np.array([self.states[i] for i in indices], dtype=np.float32),
                np.array([self.actions[i] for i in indices], dtype=np.int64),
                np.array([self.rewards[i] for i in indices], dtype=np.float32),
                np.array([self.next_states[i] for i in indices], dtype=np.float32),
                np.array([self.dones[i] for i in indices], dtype=np.bool_)
            )
    
    def clear(self) -> None:
        """Clear the buffer."""
        with self.lock:
            self.counter.value = 0
