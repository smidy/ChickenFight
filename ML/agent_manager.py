import asyncio
import random
import time
import logging
import torch
import numpy as np
from typing import Dict, List, Optional, Tuple, Any
import os
import uuid

from GameContext import GameContext
from agents.a2c_agent import A2CAgent

# Set up logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("a2c_training.log"),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

class AgentManager:
    """
    Manages multiple A2C agents for parallel training.
    Handles coordination, state synchronization, and game interactions.
    """
    
    def __init__(self,
                 server_url: str,
                 num_agents: int = 2,
                 save_dir: str = 'checkpoints',
                 save_interval: int = 50,
                 device: str = 'cuda' if torch.cuda.is_available() else 'cpu'):
        """
        Initialize the agent manager.
        
        Args:
            server_url: WebSocket URL of the game server
            num_agents: Number of agents to train in parallel
            save_dir: Directory to save model checkpoints
            save_interval: How often to save model checkpoints (in episodes)
            device: Device to use for tensor operations
        """
        self.server_url = server_url
        self.num_agents = num_agents
        self.save_dir = save_dir
        self.save_interval = save_interval
        self.device = device
        
        # Create directories
        os.makedirs(save_dir, exist_ok=True)
        
        # Initialize game contexts (one per agent)
        self.game_contexts = []
        
        # Initialize A2C agent
        self.agent = A2CAgent(
            state_dim=140,      # Updated to match actual state tensor dimension
            action_dim=16,      # Adjust based on max possible actions
            hidden_dim=256,
            card_embedding_dim=64,
            device=device
        )
        
        # Track agent pairs
        self.agent_pairs = {}  # Maps agent_id to opponent_id
        
        # Track training statistics
        self.episodes_completed = 0
        self.training_steps = 0
        self.start_time = None
        
        # Load existing model if available
        model_path = os.path.join(save_dir, 'a2c_model.pt')
        if os.path.exists(model_path):
            logger.info(f"Loading existing model from {model_path}")
            self.agent.load_model(model_path)
        
        # Track agent statuses
        self.agent_status = {}
        
        logger.info(f"AgentManager initialized with {num_agents} agents")
    
    async def initialize_agents(self):
        """Initialize all agents by creating GameContext instances."""
        logger.info("Initializing agents...")
        
        # Create game contexts
        for i in range(self.num_agents):
            agent_id = f"agent_{i}"
            game_context = GameContext(self.server_url)
            self.game_contexts.append((agent_id, game_context))
            self.agent_status[agent_id] = {
                'connected': False,
                'map_id': None,
                'in_fight': False,
                'opponent_id': None,
                'last_obs': None,
                'prev_obs': None,
                'waiting_for_opponent': False
            }
        
        # Connect all game contexts
        connect_tasks = []
        for agent_id, game_context in self.game_contexts:
            connect_tasks.append(self._connect_agent(agent_id, game_context))
        
        await asyncio.gather(*connect_tasks)
        logger.info("All agents initialized and connected")
    
    async def _connect_agent(self, agent_id: str, game_context: GameContext):
        """
        Connect an agent to the server and get a player ID.
        
        Args:
            agent_id: ID of the agent
            game_context: GameContext for the agent
        """
        try:
            logger.info(f"Connecting agent {agent_id}")
            await game_context.connect()
            
            # Request player ID
            await game_context.request_player_id()
            
            # Wait for player ID response
            for _ in range(50):  # Timeout after 5 seconds
                if game_context.player_id is not None:
                    self.agent_status[agent_id]['connected'] = True
                    logger.info(f"Agent {agent_id} connected with player ID {game_context.player_id}")
                    return
                await asyncio.sleep(0.1)
            
            logger.error(f"Timed out waiting for player ID for agent {agent_id}")
        except Exception as e:
            logger.error(f"Error connecting agent {agent_id}: {e}")
    
    async def join_maps(self):
        """Join all agents to maps."""
        logger.info("Joining maps...")
        
        # Get map list for first agent to find available maps
        first_agent_id, first_context = self.game_contexts[0]
        await first_context.request_map_list()
        
        # Wait for map list response in messages
        map_ids = []
        for _ in range(50):  # Timeout after 5 seconds
            # This is a simplification - in a real implementation we would need
            # to properly parse the map list response from the server
            # For now, we'll assume there's at least one map with ID "map_1"
            map_ids = ["map1"]
            if map_ids:
                break
            await asyncio.sleep(0.1)
        
        if not map_ids:
            logger.error("Failed to get map list")
            return
        
        # Join all agents to random maps from the list
        join_tasks = []
        for agent_id, game_context in self.game_contexts:
            # Select a random map
            map_id = random.choice(map_ids)
            join_tasks.append(self._join_map(agent_id, game_context, map_id))
        
        await asyncio.gather(*join_tasks)
        logger.info("All agents joined maps")
    
    async def _join_map(self, agent_id: str, game_context: GameContext, map_id: str):
        """
        Join an agent to a map.
        
        Args:
            agent_id: ID of the agent
            game_context: GameContext for the agent
            map_id: ID of the map to join
        """
        try:
            logger.info(f"Agent {agent_id} joining map {map_id}")
            await game_context.join_map(map_id)
            
            # Wait for join map completion
            for _ in range(50):  # Timeout after 5 seconds
                if game_context.current_map_id == map_id:
                    self.agent_status[agent_id]['map_id'] = map_id
                    logger.info(f"Agent {agent_id} joined map {map_id}")
                    return
                await asyncio.sleep(0.1)
            
            logger.error(f"Timed out waiting for agent {agent_id} to join map {map_id}")
        except Exception as e:
            logger.error(f"Error joining map for agent {agent_id}: {e}")
    
    async def pair_agents(self):
        """Pair agents for fights."""
        logger.info("Pairing agents for fights...")
        
        # Get list of agent IDs that are on maps but not in fights
        available_agents = [
            agent_id for agent_id, status in self.agent_status.items()
            if status['connected'] and status['map_id'] and not status['in_fight']
            and not status['waiting_for_opponent']
        ]
        
        # Shuffle agents to randomize pairings
        random.shuffle(available_agents)
        
        # Pair agents
        while len(available_agents) >= 2:
            agent1_id = available_agents.pop()
            agent2_id = available_agents.pop()
            
            self.agent_pairs[agent1_id] = agent2_id
            self.agent_pairs[agent2_id] = agent1_id
            
            # Mark agents as waiting for opponent
            self.agent_status[agent1_id]['waiting_for_opponent'] = True
            self.agent_status[agent2_id]['waiting_for_opponent'] = True
            
            # Get game contexts
            agent1_context = next(ctx for aid, ctx in self.game_contexts if aid == agent1_id)
            
            # Have agent1 challenge agent2
            logger.info(f"Agent {agent1_id} challenging agent {agent2_id}")
            
            # Find the other player ID in the game
            agent2_player_id = next(
                ctx.player_id for aid, ctx in self.game_contexts if aid == agent2_id
            )
            
            # Send fight challenge
            challenge_request = {
                "MessageType": "ExtFightChallengeRequest",
                "TargetId": agent2_player_id
            }
            await agent1_context.send(challenge_request)
            
            # Have agent2 accept the challenge
            agent2_context = next(ctx for aid, ctx in self.game_contexts if aid == agent2_id)
            
            # Get agent1's player ID
            agent1_player_id = agent1_context.player_id
            
            # Accept challenge
            accept_request = {
                "MessageType": "ExtFightChallengeAccepted",
                "TargetId": agent1_player_id
            }
            await agent2_context.send(accept_request)
            
            logger.info(f"Paired agents {agent1_id} and {agent2_id} for fight")
        
        logger.info("Agent pairing complete")
    
    async def train_agents(self, num_episodes: int = 1000):
        """
        Train agents for a specified number of episodes.
        
        Args:
            num_episodes: Number of episodes to train for
        """
        logger.info(f"Starting training for {num_episodes} episodes")
        self.start_time = time.time()
        
        episode = 0
        while episode < num_episodes:
            # Pair any unpaired agents
            await self.pair_agents()
            
            # Process one step for each agent
            step_tasks = []
            for agent_id, game_context in self.game_contexts:
                step_tasks.append(self._process_agent_step(agent_id, game_context))
            
            await asyncio.gather(*step_tasks)
            
            # Check for completed episodes
            completed_episodes = self._check_completed_episodes()
            episode += completed_episodes
            
            # Update training statistics
            if completed_episodes > 0:
                self.episodes_completed += completed_episodes
                
                # Train agent on collected experiences
                train_stats = self.agent.train()
                self.training_steps += 1
                
                # Log statistics
                self._log_training_stats(train_stats)
                
                # Save model periodically
                if self.episodes_completed % self.save_interval == 0:
                    self._save_model()
        
        logger.info(f"Training completed after {self.episodes_completed} episodes")
        self._save_model()
    
    async def _process_agent_step(self, agent_id: str, game_context: GameContext):
        """
        Process one step for an agent.
        
        Args:
            agent_id: ID of the agent
            game_context: GameContext for the agent
        """
        status = self.agent_status[agent_id]
        
        # Check if agent is in a fight
        if game_context.is_in_fight:
            # Agent is in a fight, so update fight status
            if not status['in_fight']:
                logger.info(f"Agent {agent_id} entered fight with {game_context.opponent_id}")
                status['in_fight'] = True
                status['opponent_id'] = game_context.opponent_id
                status['waiting_for_opponent'] = False
            
            # Update observations
            status['prev_obs'] = status['last_obs']
            status['last_obs'] = game_context.get_observation()
            
            # Calculate reward if we have a previous observation
            reward = 0.0
            if status['prev_obs']:
                reward = game_context.get_reward(status['prev_obs'])
                self.agent.add_reward(reward)
            
            # Check if it's the agent's turn
            if game_context.is_player_turn:
                # Get valid actions
                valid_actions = game_context.get_valid_actions()
                
                # Select action using the agent
                action_idx, action = self.agent.select_action(status['last_obs'], valid_actions)
                
                # Execute the action
                if action['type'] == 'play_card':
                    await game_context.play_card(action['card_id'])
                    logger.debug(f"Agent {agent_id} played card {action['card_id']}")
                elif action['type'] == 'end_turn':
                    await game_context.end_turn()
                    logger.debug(f"Agent {agent_id} ended turn")
        else:
            # Agent is not in a fight
            if status['in_fight']:
                # Fight just ended
                logger.info(f"Fight ended for agent {agent_id}")
                
                # Calculate final reward
                if status['last_obs']:
                    final_reward = game_context.get_reward(status['last_obs'])
                    self.agent.add_reward(final_reward, done=True)
                
                # Reset fight status
                status['in_fight'] = False
                status['opponent_id'] = None
                status['waiting_for_opponent'] = False
                status['last_obs'] = None
                status['prev_obs'] = None
                
                # If we had an opponent, reset their pairing too
                if agent_id in self.agent_pairs:
                    opponent_id = self.agent_pairs[agent_id]
                    self.agent_pairs.pop(agent_id, None)
                    self.agent_pairs.pop(opponent_id, None)
                    self.agent_status[opponent_id]['waiting_for_opponent'] = False
            
            # If not waiting for an opponent, try to find one
            if not status['waiting_for_opponent'] and not status['in_fight']:
                # Look for players not in fights
                available_players = [
                    player_id for player_id in game_context.other_player_info
                    if player_id not in game_context.players_in_fight
                ]
                
                if available_players:
                    # Challenge a random player
                    target_id = random.choice(available_players)
                    challenge_request = {
                        "MessageType": "ExtFightChallengeRequest",
                        "TargetId": target_id
                    }
                    await game_context.send(challenge_request)
                    status['waiting_for_opponent'] = True
                    logger.debug(f"Agent {agent_id} challenged player {target_id}")
    
    def _check_completed_episodes(self) -> int:
        """
        Check for completed episodes and return the count.
        
        Returns:
            int: Number of completed episodes
        """
        # This is a simplified implementation that checks for completed fights
        # In a real implementation, we might want more sophisticated tracking
        
        # Count agent pairs that have been reset (fights ended)
        completed_fights = 0
        for agent_id, status in self.agent_status.items():
            if agent_id not in self.agent_pairs and not status['in_fight'] and not status['waiting_for_opponent']:
                # This agent was in a fight but is no longer paired
                completed_fights += 1
        
        # Each fight involves 2 agents, so divide by 2
        return completed_fights // 2
    
    def _log_training_stats(self, train_stats: Dict):
        """
        Log training statistics.
        
        Args:
            train_stats: Dictionary of training statistics
        """
        # Get agent stats
        agent_stats = self.agent.get_stats()
        
        # Calculate elapsed time
        elapsed_time = time.time() - self.start_time
        
        # Log stats
        logger.info(
            f"Episodes: {self.episodes_completed}, "
            f"Training steps: {self.training_steps}, "
            f"Win rate: {agent_stats['win_rate']:.2f}, "
            f"Avg reward: {agent_stats['avg_reward']:.2f}, "
            f"Policy loss: {train_stats['policy_loss']:.4f}, "
            f"Value loss: {train_stats['value_loss']:.4f}, "
            f"Total loss: {train_stats['total_loss']:.4f}, "
            f"Time: {elapsed_time:.1f}s"
        )
    
    def _save_model(self):
        """Save the current model to a file."""
        model_path = os.path.join(self.save_dir, 'a2c_model.pt')
        self.agent.save_model(model_path)
        logger.info(f"Model saved to {model_path}")
    
    async def close(self):
        """Close all connections and clean up resources."""
        logger.info("Closing all connections...")
        
        close_tasks = []
        for _, game_context in self.game_contexts:
            close_tasks.append(game_context.close())
        
        await asyncio.gather(*close_tasks)
        logger.info("All connections closed")
