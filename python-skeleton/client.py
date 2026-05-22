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
import urllib.error
import urllib.request
import websockets

from game_state import GameState
from strategy import decide_actions

# ------------------------------------------------------------------ #
# Configuration – change TANK_ID to your chosen player name
# ------------------------------------------------------------------ #
SERVER_URI = "ws://192.168.1.90:8080/"
REST_BASE  = "http://192.168.1.90:8080"
TANK_ID = "piithon"


# ------------------------------------------------------------------ #
# REST helpers – blocking HTTP GET (call sparingly, not every turn)
# ------------------------------------------------------------------ #

def _get(path: str) -> dict:
    with urllib.request.urlopen(REST_BASE + path) as r:
        return json.loads(r.read())

def fetch_map() -> dict:
    """Wall list and gridSize.
    Returns: {gridSize, walls: [{x, y, orientation}]}"""
    return _get("/map")

def fetch_players() -> list[dict]:
    """All living players' positions and turret angles.
    Returns: [{tankId, gridX, gridY, posX, posY, turretDegrees}]"""
    return _get("/players")["players"]

def fetch_player(tank_id: str) -> dict | None:
    """Single player state, or None if not found.
    Returns: {tankId, gridX, gridY, posX, posY, turretDegrees}"""
    try:
        return _get(f"/player/{tank_id}")
    except urllib.error.HTTPError:
        return None

def fetch_state() -> dict:
    """Game status.
    Returns: {gameStarted, gameOver, onTurn, round, scores: {tankId: pts}}"""
    return _get("/state")

def fetch_constants() -> dict:
    """Physics constants for bullet trajectory math.
    Returns: {gridSize, bulletSpeed, bulletMaxBounces,
              tankBodySize, tankBodyHalfSize, muzzleOffset, bulletRadius}"""
    return _get("/constants")

def fetch_turn_time() -> dict:
    """Seconds remaining in the current turn.
    Returns: {turnTimeRemainingSeconds: float, onTurn: str}"""
    return _get("/turn_time")


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
        players = fetch_players()
        state.handle_players_snapshot(players)
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

    elif event == "turn_timeout":
        tank_id = data["tankId"]
        state.handle_turn_timeout(tank_id)
        print(f"[timeout] {tank_id} timed out — turn skipped (1st warning)")

    elif event == "turn_disqualified":
        tank_id = data["tankId"]
        state.handle_turn_disqualified(tank_id)
        print(f"[disqualified] {tank_id} timed out twice — eliminated")

    elif event == "game_over":
        state.handle_game_over(data.get("winner", ""))
        msg = f"[game] {state.winner} wins!" if state.winner else "[game] Draw!"
        scores = data.get("scores", {})
        scores_str = "  ".join(f"{k}: {v}" for k, v in scores.items())
        print(msg + f" Round {data.get('round', '?')} | Scores: {scores_str} | Waiting for next round...")

    elif event == "lobby_reset":
        print("[lobby] Server reset lobby — reconnecting...")

    else:
        print(f"[unknown event] {data}")


# ------------------------------------------------------------------ #
# Main loop
# ------------------------------------------------------------------ #

async def run() -> None:
    while True:
        try:
            async with websockets.connect(SERVER_URI) as ws:
                print(f"Connected to {SERVER_URI} as '{TANK_ID}'")
                state = GameState(my_tank_id=TANK_ID)

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

            print("Disconnected. Reconnecting in 2s...")
        except OSError as e:
            print(f"[error] {e} — retrying in 2s...")
        await asyncio.sleep(2)


if __name__ == "__main__":
    asyncio.run(run())
