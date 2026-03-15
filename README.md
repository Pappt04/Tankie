# Tankie

A turn-based multiplayer 2D tank battle game. Two AI clients compete on a procedurally generated maze. You write the strategy; the server runs the game.

## How it works

1. Start the Godot server
2. Connect your client(s) and join with a name
3. Press **Start** in the game window
4. The server broadcasts the map, then turns begin
5. Each turn the active player sends their actions; the server validates and executes them
6. Last tank standing wins

---

## Requirements

| Component | Requirement |
|-----------|-------------|
| Game server | Godot 4.6 |
| Python client | Python 3.11+, `pip install websockets` |
| C# client | .NET 8.0 SDK |
| C++ client | CMake 3.15+, a C++17 compiler |

---

## Running the server

Open the `tankie/` folder in Godot 4.6 and press **F5**, or from the command line:

```
cd tankie
godot --headless    # no window
godot               # with game window
```

The WebSocket server starts automatically on `ws://localhost:8080/`.

---

## Client skeletons

Each skeleton has the same structure: a networking layer, a `GameState` that tracks the world, and a `Strategy` stub where you write your logic.

### Python

```bash
cd python-skeleton
pip install websockets
python client.py
```

Edit `TANK_ID` in `client.py`, then implement `decide_actions()` in `strategy.py`.

### C#

```bash
cd csharp-skeleton/TankieClient
dotnet run
```

Edit `TankId` in `Program.cs`, then implement `DecideActions()` in `Strategy.cs`.

### C++

```bash
cd cpp-skeleton
cmake -B build && cmake --build build
./build/tankie_client
```

Edit `TANK_ID` in `main.cpp`, then implement `decide_actions()` in `strategy.hpp`.

---

## Game rules

- Two players take turns. On your turn you submit a list of actions.
- **Action budget per turn:**
  - Up to 2 moves, OR
  - 1 move + 1 shoot, OR
  - 1 shoot only
  - Rotate actions are free and do not count against the budget
- Submitting an over-budget command is silently rejected — the server skips your turn.
- A bullet bounces up to **2 times** off walls before despawning.
- A bullet that hits a tank eliminates it immediately.
- Last tank alive wins. If both are eliminated on the same shot, it is a draw.

---

## Coordinate system

The map is a grid of cells. Each cell is `gridSize` pixels wide (default **128 px**).

```
(0,0) ── x ──▶
  │
  y
  │
  ▼
```

**World position** (what `moved` events report): center of the cell → `world = grid * gridSize + gridSize / 2`

**Grid position** (what the `map` event and your state track): integer cell index → `grid = floor(world / gridSize)`

### Walls

A wall sits on the **edge** between two cells.

| Orientation | Position `(x, y)` | Separates |
|-------------|-------------------|-----------|
| `HORIZONTAL` | `(x, y)` | cell `(x, y)` from cell `(x, y+1)` — wall on the **bottom** edge of `(x, y)` |
| `VERTICAL`   | `(x, y)` | cell `(x, y)` from cell `(x+1, y)` — wall on the **right** edge of `(x, y)` |

Boundary walls use out-of-range coordinates (e.g. `y = -1` for the top border).

Check for a wall in all three clients:

```python
state.has_wall(x, y, "HORIZONTAL")   # Python
state.HasWall(x, y, "HORIZONTAL")    // C#
state.has_wall(x, y, "HORIZONTAL")   // C++
```

---

## WebSocket protocol

All messages are UTF-8 JSON text frames.

### Client → Server

**Join** (send once before the game starts):
```json
{ "action": "join", "tankId": "my_name" }
```

**Submit actions** (send on your turn):
```json
{
  "tankId": "my_name",
  "actions": [
    { "type": "move",   "direction": "up" },
    { "type": "rotate", "degrees": 90 },
    { "type": "shoot" }
  ]
}
```

`direction` values: `"up"`, `"down"`, `"left"`, `"right"`

### Server → Client (events)

| Event | Payload fields | When |
|-------|---------------|------|
| `player_joined` | `tankId` | A player joined the lobby |
| `map` | `gridWidth`, `gridHeight`, `gridSize`, `walls[]`, `players[]` | Once, just before `game_started` |
| `game_started` | `onTurn` | Countdown finished, game begins |
| `turn_changed` | `nextTurn` | A turn was completed |
| `moved` | `tankId`, `direction`, `pos_x`, `pos_y` | A tank moved (world coords) |
| `rotated` | `tankId`, `degrees` | A tank's turret rotated |
| `shot` | `tankId` | A tank fired |
| `game_over` | `winner` | Game ended (`winner` is empty string on draw) |

#### `map` event detail

```json
{
  "event": "map",
  "gridWidth": 10,
  "gridHeight": 8,
  "gridSize": 128,
  "walls": [
    { "x": 0, "y": -1, "orientation": "HORIZONTAL" },
    { "x": 3, "y": 2,  "orientation": "VERTICAL" }
  ],
  "players": [
    { "tankId": "alice", "x": 1, "y": 1 },
    { "tankId": "bob",   "x": 8, "y": 6 }
  ]
}
```

---

## Implementing your strategy

The only file you need to edit is the strategy file for your language. It receives a `GameState` snapshot and returns a list of actions.

```python
# python-skeleton/strategy.py
def decide_actions(state: GameState) -> list[dict]:
    me = state.my_tank()         # my position, turret angle
    enemies = state.opponents()  # list of opponent TankStates

    # state.has_wall(x, y, "HORIZONTAL")  – wall lookup
    # state.grid_width, state.grid_height – map dimensions
    # state.grid_size                     – pixels per cell

    return [{"type": "move", "direction": "up"}]
```

The same fields and helpers are available in the C# and C++ versions.

---

## Project structure

```
tankie/               Godot 4.6 game server (C#)
  logic/
    GameServer.cs     WebSocket server, message queue
    GameManager.cs    Turn loop, maze generation, event broadcasting
    GlobalState.cs    Shared wall registry, connected players
    Tank.cs           Movement, shooting, collision
    Bullet.cs         Projectile with bounce logic

python-skeleton/
  client.py           WebSocket connection and event dispatcher
  game_state.py       GameState and TankState classes
  strategy.py         ← implement your strategy here

csharp-skeleton/TankieClient/
  Program.cs          WebSocket connection and event dispatcher
  GameState.cs        GameState and TankState classes
  Strategy.cs         ← implement your strategy here

cpp-skeleton/
  main.cpp            Entry point
  client.cpp          Event dispatcher
  game_state.hpp      GameState and TankState
  strategy.hpp        ← implement your strategy here
  json_parser.cpp/hpp Minimal JSON parser
  websocket.cpp/hpp   Raw WebSocket over Asio
```
