"""
WebSocket client for communication with the game server.
"""
import json
import logging
import threading
import time
from typing import Dict, Any, Optional, Callable, List, Union, Tuple
from queue import Queue, Empty

import websocket

from ..utils.config import ServerConfig


class WebSocketClient:
    """
    WebSocket client for communication with the game server.
    """
    
    def __init__(self, server_config: ServerConfig):
        """
        Initialize the WebSocket client.
        
        Args:
            server_config: Server configuration
        """
        self.server_config = server_config
        self.url = server_config.url
        
        # Set up logging
        self.logger = logging.getLogger("WebSocketClient")
        self.logger.setLevel(logging.INFO)
        
        # Message handling
        self.message_queue = Queue()
        self.response_queues: Dict[str, Queue] = {}
        self.message_handlers: Dict[str, Callable] = {}
        
        # WebSocket connection
        self.ws: Optional[websocket.WebSocketApp] = None
        self.connected = False
        self.reconnect_delay = 1.0  # Initial reconnect delay in seconds
        self.max_reconnect_delay = 30.0  # Maximum reconnect delay in seconds
        
        # Threading
        self.thread: Optional[threading.Thread] = None
        self.running = False
    
    def connect(self) -> bool:
        """
        Connect to the WebSocket server.
        
        Returns:
            True if connection was successful, False otherwise
        """
        if self.connected:
            self.logger.warning("Already connected to the server")
            return True
        
        self.logger.info(f"Connecting to {self.url}")
        
        # Initialize WebSocket
        self.ws = websocket.WebSocketApp(
            self.url,
            on_open=self._on_open,
            on_message=self._on_message,
            on_error=self._on_error,
            on_close=self._on_close
        )
        
        # Start WebSocket thread
        self.running = True
        self.thread = threading.Thread(target=self._run_websocket)
        self.thread.daemon = True
        self.thread.start()
        
        # Wait for connection to establish
        timeout = 10.0  # seconds
        start_time = time.time()
        while not self.connected and time.time() - start_time < timeout:
            time.sleep(0.1)
        
        if not self.connected:
            self.logger.error(f"Failed to connect to {self.url} within {timeout} seconds")
            return False
        
        return True
    
    def disconnect(self) -> None:
        """
        Disconnect from the WebSocket server.
        """
        if not self.connected or self.ws is None:
            self.logger.warning("Not connected to the server")
            return
        
        self.logger.info("Disconnecting from the server")
        self.running = False
        self.ws.close()
        
        if self.thread is not None:
            self.thread.join(timeout=2.0)
            if self.thread.is_alive():
                self.logger.warning("WebSocket thread did not terminate properly")
    
    def send_message(self, message: Dict[str, Any], wait_for_response: bool = False,
                    response_type: Optional[str] = None, timeout: float = 5.0) -> Optional[Dict[str, Any]]:
        """
        Send a message to the server.
        
        Args:
            message: Message to send
            wait_for_response: Whether to wait for a response
            response_type: Expected response message type
            timeout: Timeout for waiting for response in seconds
            
        Returns:
            Response message if wait_for_response is True, otherwise None
        """
        if not self.connected or self.ws is None:
            self.logger.error("Not connected to the server")
            return None
        
        # Ensure message has a MessageType
        if "MessageType" not in message:
            self.logger.error("Message must have a MessageType")
            return None
        
        message_type = message["MessageType"]
        self.logger.debug(f"Sending message: {message_type}")
        
        # Set up response queue if waiting for response
        response_queue = None
        if wait_for_response:
            response_queue = Queue()
            if response_type is None:
                # Default response type is the message type with "Response" suffix
                response_type = f"{message_type}Response"
            self.response_queues[response_type] = response_queue
        
        # Send message
        try:
            self.ws.send(json.dumps(message))
        except Exception as e:
            self.logger.error(f"Error sending message: {e}")
            if wait_for_response and response_type in self.response_queues:
                del self.response_queues[response_type]
            return None
        
        # Wait for response if needed
        if wait_for_response and response_queue is not None:
            try:
                response = response_queue.get(timeout=timeout)
                del self.response_queues[response_type]
                return response
            except Empty:
                self.logger.error(f"Timeout waiting for response to {message_type}")
                if response_type in self.response_queues:
                    del self.response_queues[response_type]
                return None
        
        return None
    
    def register_handler(self, message_type: str, handler: Callable[[Dict[str, Any]], None]) -> None:
        """
        Register a handler for a specific message type.
        
        Args:
            message_type: Type of message to handle
            handler: Function to call when a message of this type is received
        """
        self.message_handlers[message_type] = handler
        self.logger.debug(f"Registered handler for message type: {message_type}")
    
    def unregister_handler(self, message_type: str) -> None:
        """
        Unregister a handler for a specific message type.
        
        Args:
            message_type: Type of message to unregister handler for
        """
        if message_type in self.message_handlers:
            del self.message_handlers[message_type]
            self.logger.debug(f"Unregistered handler for message type: {message_type}")
    
    def _run_websocket(self) -> None:
        """
        Run the WebSocket connection in a loop with reconnection.
        """
        reconnect_delay = self.reconnect_delay
        
        while self.running:
            try:
                self.ws.run_forever()
                
                # If we get here, the connection was closed
                if self.running:
                    self.logger.info(f"Connection closed, reconnecting in {reconnect_delay} seconds")
                    time.sleep(reconnect_delay)
                    reconnect_delay = min(reconnect_delay * 2, self.max_reconnect_delay)
                else:
                    break
            except Exception as e:
                self.logger.error(f"Error in WebSocket thread: {e}")
                if self.running:
                    self.logger.info(f"Reconnecting in {reconnect_delay} seconds")
                    time.sleep(reconnect_delay)
                    reconnect_delay = min(reconnect_delay * 2, self.max_reconnect_delay)
                else:
                    break
    
    def _on_open(self, ws: websocket.WebSocketApp) -> None:
        """
        Called when the WebSocket connection is established.
        
        Args:
            ws: WebSocket instance
        """
        self.connected = True
        self.logger.info(f"Connected to {self.url}")
    
    def _on_message(self, ws: websocket.WebSocketApp, message: str) -> None:
        """
        Called when a message is received from the server.
        
        Args:
            ws: WebSocket instance
            message: Received message
        """
        try:
            data = json.loads(message)
            
            # Check if message has a type
            if "MessageType" not in data:
                self.logger.warning(f"Received message without MessageType: {data}")
                return
            
            message_type = data["MessageType"]
            self.logger.debug(f"Received message: {message_type}")
            
            # Check if this is a response to a waiting request
            if message_type in self.response_queues:
                self.response_queues[message_type].put(data)
            
            # Check if there's a handler for this message type
            if message_type in self.message_handlers:
                try:
                    self.message_handlers[message_type](data)
                except Exception as e:
                    self.logger.error(f"Error in message handler for {message_type}: {e}")
            
            # Add to general message queue
            self.message_queue.put(data)
            
        except json.JSONDecodeError:
            self.logger.error(f"Received invalid JSON: {message}")
        except Exception as e:
            self.logger.error(f"Error processing message: {e}")
    
    def _on_error(self, ws: websocket.WebSocketApp, error: Exception) -> None:
        """
        Called when a WebSocket error occurs.
        
        Args:
            ws: WebSocket instance
            error: Error that occurred
        """
        self.logger.error(f"WebSocket error: {error}")
    
    def _on_close(self, ws: websocket.WebSocketApp, close_status_code: Optional[int],
                 close_msg: Optional[str]) -> None:
        """
        Called when the WebSocket connection is closed.
        
        Args:
            ws: WebSocket instance
            close_status_code: Status code for the close
            close_msg: Close message
        """
        self.connected = False
        self.logger.info(f"Connection closed: {close_status_code} {close_msg}")
    
    def get_next_message(self, timeout: Optional[float] = None) -> Optional[Dict[str, Any]]:
        """
        Get the next message from the queue.
        
        Args:
            timeout: Timeout for waiting for a message in seconds
            
        Returns:
            Next message or None if timeout
        """
        try:
            return self.message_queue.get(timeout=timeout)
        except Empty:
            return None
