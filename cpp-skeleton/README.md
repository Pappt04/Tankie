# Tankie – C++ Client

## Requirements

- A C++17 compiler (`g++` or `clang++`)
- `make`
- POSIX threads (included on Linux/macOS)

No external libraries need to be installed — Asio is bundled in `asio-1.36.0/`.

## Build

```bash
make
```

The binary is written to `build/tankie_client`. To rebuild from scratch:

```bash
make clean && make
```

To use a different compiler:

```bash
CXX=clang++ make
```

## Run

Start the Godot game server first and leave it on the **Menu** screen, then:

```bash
./build/tankie_client
```

The client connects to `ws://localhost:8080`, sends a join request, and waits for the game to start. Once the server operator presses **Start**, the game begins. After a round ends the client stays connected and automatically plays the next round when **Enter** is pressed in-game.

## Configuration

Edit the constants at the top of `main.cpp`:

| Constant  | Default       | Meaning                        |
|-----------|---------------|--------------------------------|
| `HOST`    | `localhost`   | Server hostname or IP          |
| `PORT`    | `8080`        | Server port                    |
| `TANK_ID` | `player1`     | Name shown in-game for your tank |

## Writing your strategy

All game logic lives in **`strategy.hpp`** — only `decide_actions()` needs to be changed.

```cpp
inline std::string decide_actions(const GameState& state) {
    // Build and return a JSON action array, e.g.:
    return "[" + actions::rotate(90) + "," + actions::move("up") + "]";
}
```

**Action budget per turn:**
- Up to **2 MOVE** actions, or
- **1 MOVE + 1 SHOOT**, or
- **1 SHOOT** only
- Any number of **ROTATE** actions (free, no budget cost)

Helper functions in the `actions::` namespace:

```cpp
actions::move("up")      // direction: up / down / left / right
actions::rotate(90)      // degrees: absolute turret angle (0 = right)
actions::shoot()
```

Use `state` to query the current situation:

```cpp
state.my_tank()          // your tank's position, turret angle, alive flag
state.opponents()        // vector of pointers to enemy TankState
state.has_wall(x, y, "HORIZONTAL")  // wall collision check
state.grid_width() / state.grid_height() / state.grid_size()
state.is_my_turn()
```

## File overview

| File | Purpose |
|------|---------|
| `main.cpp` | Entry point, connect/receive loop |
| `client.cpp` / `client.hpp` | Event dispatcher (`dispatch()`) |
| `game_state.hpp` | Local state mirror, updated from server events |
| `tank_state.hpp` | Per-tank data struct |
| `strategy.hpp` | **Your strategy goes here** |
| `websocket.cpp` / `websocket.hpp` | Raw WebSocket framing (RFC 6455) |
| `json_parser.cpp` / `json_parser.hpp` | Minimal JSON field extractor |
| `asio-1.36.0/` | Bundled standalone Asio (networking) |
