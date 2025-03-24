"""
Configuration management for the AI training solution.
"""
import os
import json
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Union, Any


@dataclass
class ServerConfig:
    """Configuration for the game server connection."""
    host: str = "127.0.0.1"
    port: int = 8080
    use_ssl: bool = False
    
    @property
    def url(self) -> str:
        """Get the WebSocket URL for the server."""
        protocol = "wss" if self.use_ssl else "ws"
        return f"{protocol}://{self.host}:{self.port}"


@dataclass
class TrainingConfig:
    """Configuration for the training process."""
    # General training parameters
    num_agents: int = 4
    total_episodes: int = 10000
    save_interval: int = 100
    eval_interval: int = 50
    checkpoint_dir: str = "checkpoints"
    
    # A2C hyperparameters
    learning_rate: float = 0.001
    gamma: float = 0.99  # Discount factor
    entropy_coef: float = 0.01
    value_loss_coef: float = 0.5
    max_grad_norm: float = 0.5
    
    # Exploration parameters
    initial_epsilon: float = 1.0
    final_epsilon: float = 0.05
    epsilon_decay_episodes: int = 5000
    
    # Experience replay
    use_replay_buffer: bool = True
    replay_buffer_size: int = 10000
    batch_size: int = 64


@dataclass
class ModelConfig:
    """Configuration for the neural network model."""
    # Network architecture
    state_dim: int = 128  # Dimension of the state representation
    hidden_dim: int = 256
    num_layers: int = 2
    
    # CUDA settings
    use_cuda: bool = True
    cuda_device: int = 0


@dataclass
class Config:
    """Main configuration class that contains all settings."""
    server: ServerConfig = field(default_factory=ServerConfig)
    training: TrainingConfig = field(default_factory=TrainingConfig)
    model: ModelConfig = field(default_factory=ModelConfig)
    
    @classmethod
    def from_dict(cls, config_dict: Dict[str, Any]) -> 'Config':
        """Create a Config instance from a dictionary."""
        server_config = ServerConfig(**config_dict.get('server', {}))
        training_config = TrainingConfig(**config_dict.get('training', {}))
        model_config = ModelConfig(**config_dict.get('model', {}))
        
        return cls(
            server=server_config,
            training=training_config,
            model=model_config
        )
    
    @classmethod
    def from_json(cls, json_path: str) -> 'Config':
        """Load configuration from a JSON file."""
        if not os.path.exists(json_path):
            raise FileNotFoundError(f"Config file not found: {json_path}")
        
        with open(json_path, 'r') as f:
            config_dict = json.load(f)
        
        return cls.from_dict(config_dict)
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert the configuration to a dictionary."""
        return {
            'server': {
                'host': self.server.host,
                'port': self.server.port,
                'use_ssl': self.server.use_ssl
            },
            'training': {
                'num_agents': self.training.num_agents,
                'total_episodes': self.training.total_episodes,
                'save_interval': self.training.save_interval,
                'eval_interval': self.training.eval_interval,
                'checkpoint_dir': self.training.checkpoint_dir,
                'learning_rate': self.training.learning_rate,
                'gamma': self.training.gamma,
                'entropy_coef': self.training.entropy_coef,
                'value_loss_coef': self.training.value_loss_coef,
                'max_grad_norm': self.training.max_grad_norm,
                'initial_epsilon': self.training.initial_epsilon,
                'final_epsilon': self.training.final_epsilon,
                'epsilon_decay_episodes': self.training.epsilon_decay_episodes,
                'use_replay_buffer': self.training.use_replay_buffer,
                'replay_buffer_size': self.training.replay_buffer_size,
                'batch_size': self.training.batch_size
            },
            'model': {
                'state_dim': self.model.state_dim,
                'hidden_dim': self.model.hidden_dim,
                'num_layers': self.model.num_layers,
                'use_cuda': self.model.use_cuda,
                'cuda_device': self.model.cuda_device
            }
        }
    
    def save(self, json_path: str) -> None:
        """Save the configuration to a JSON file."""
        with open(json_path, 'w') as f:
            json.dump(self.to_dict(), f, indent=4)


# Default configuration
DEFAULT_CONFIG = Config()
