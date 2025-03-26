#!/usr/bin/env python3
"""
A simple script to test the GameContext class by connecting to the server,
sending a player ID request, and receiving the response.
"""

import asyncio
import json
import sys
import time
from GameContext import GameContext

# Flag to track if we've received the response
response_received = False

# Create a callback function to handle server messages
def message_handler(message):
    global response_received
    
    print(f"Received message: {message.get('MessageType')}")
    
    if message.get("MessageType") == "ExtPlayerIdResponse":
        player_id = message.get("PlayerId")
        print(f"Received player ID: {player_id}")
        response_received = True
        
        # Signal the main loop to exit
        asyncio.create_task(stop_program())

async def stop_program():
    # Wait a moment to ensure message is processed
    await asyncio.sleep(1)
    # Stop the event loop
    asyncio.get_running_loop().stop()

async def main():
    # Server URL - assuming default WebSocket server on localhost
    server_url = "ws://127.0.0.1:8080"
    
    try:
        # Create a GameContext instance
        game_context = GameContext(server_url)
        
        # Register the message handler
        game_context.add_server_message_callback(message_handler)
        
        # Connect to the server
        print(f"Connecting to server at {server_url}...")
        await game_context.connect()
        print("Connected successfully")
        
        # Create player ID request message
        player_id_request = {
            "MessageType": "ExtPlayerIdRequest"
        }
        
        # Send the message
        print("Sending player ID request...")
        success = await game_context.send(player_id_request)
        if success:
            print("Request sent successfully, waiting for response...")
        else:
            print("Failed to send request")
            await game_context.close()
            return
        
        # Set a timeout for receiving the response
        timeout = 10  # seconds
        start_time = asyncio.get_event_loop().time()
        
        # Wait for the response or timeout
        while not response_received:
            await asyncio.sleep(0.1)
            elapsed = asyncio.get_event_loop().time() - start_time
            if elapsed > timeout:
                print(f"Timeout after {timeout} seconds waiting for response")
                break
        
    except Exception as e:
        print(f"Error: {e}")
        if hasattr(e, "__traceback__"):
            import traceback
            traceback.print_tb(e.__traceback__)
    finally:
        # Clean up
        if 'game_context' in locals():
            await game_context.close()
            print("Connection closed")

if __name__ == "__main__":
    try:
        # Run the main function
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\nProgram interrupted by user")
        sys.exit(0)
