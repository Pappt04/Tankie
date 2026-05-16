"""
Tankie – Python WebSocket client skeleton.

Usage
-----
    pip install -r requirements.txt
    python client.py

Make sure the Godot game is running and showing the Menu screen before
you connect.  Press "Start" in-game once all players have joined.
"""

import asyncio
import json
import websockets

from game_state import GameState
from strategy import decide_actions

# ------------------------------------------------------------------ #
# Configuration – change TANK_ID to your chosen player name
# ------------------------------------------------------------------ #
SERVER_URI = "ws://localhost:8080/"
TANK_ID = "player1"


# ------------------------------------------------------------------ #
# Message helpers
# ------------------------------------------------------------------ #

def make_join(tank_id: str) -> str:
    return json.dumps({"action": "join", "tankId": tank_id})


def make_command(tank_id: str, actions: list[dict]) -> str:
    return json.dumps({"tankId": tank_id, "actions": actions})


# ------------------------------------------------------------------ #
# Event dispatcher
# ------------------------------------------------------------------ #

def dispatch(data: dict, state: GameState) -> None:
    """Update local state based on an incoming server event."""
    event = data.get("event")

    if event == "player_joined":
        state.handle_player_joined(data["tankId"])
        print(f"[lobby] {data['tankId']} joined")

    elif event == "map":
        state.handle_map(data)
        print(f"[map] received {len(data['walls'])} walls, grid {data['gridWidth']}x{data['gridHeight']}")

    elif event == "game_started":
        state.handle_game_started(data["onTurn"])
        print(f"[game] started – first turn: {data['onTurn']}")

    elif event == "turn_changed":
        state.handle_turn_changed(data["nextTurn"])
        print(f"[game] turn → {data['nextTurn']}")

    elif event == "moved":
        state.handle_moved(data["tankId"], data["direction"], data["pos_x"], data["pos_y"])
        print(f"[move] {data['tankId']} → {data['direction']} ({data['pos_x']}, {data['pos_y']})")

    elif event == "rotated":
        state.handle_rotated(data["tankId"], data["degrees"])
        print(f"[rotate] {data['tankId']} turret → {data['degrees']}°")

    elif event == "shot":
        print(f"[shot] {data['tankId']} fired")

    elif event == "game_over":
        state.handle_game_over(data.get("winner", ""))
        msg = f"[game] {state.winner} wins!" if state.winner else "[game] Draw!"
        print(msg + " Waiting for next round...")

    else:
        print(f"[unknown event] {data}")


# ------------------------------------------------------------------ #
# Main loop
# ------------------------------------------------------------------ #

async def run() -> None:
    state = GameState(my_tank_id=TANK_ID)

    async with websockets.connect(SERVER_URI) as ws:
        print(f"Connected to {SERVER_URI} as '{TANK_ID}'")

        # Join the game
        await ws.send(make_join(TANK_ID))

        async for raw in ws:
            data = json.loads(raw)
            dispatch(data, state)

            # Act when it is our turn
            if state.is_my_turn:
                actions = decide_actions(state)
                print(f"[action] sending {actions}")
                await ws.send(make_command(TANK_ID, actions))

    print("Disconnected.")


if __name__ == "__main__":
    asyncio.run(run())
