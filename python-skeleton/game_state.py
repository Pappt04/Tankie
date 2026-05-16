"""
Tankie – local game state tracker.

Keep this updated as events arrive from the server so your strategy
can make informed decisions without blind guessing.
"""

from dataclasses import dataclass, field


@dataclass
class TankState:
    tank_id: str
    pos_x: float = 0.0
    pos_y: float = 0.0
    grid_x: int = 0
    grid_y: int = 0
    turret_degrees: float = 0.0
    alive: bool = True


@dataclass
class GameState:
    my_tank_id: str
    tanks: dict[str, TankState] = field(default_factory=dict)
    on_turn: str = ""
    game_started: bool = False
    game_over: bool = False
    winner: str | None = None

    # Map data – populated by the "map" event at game start
    grid_width: int = 0
    grid_height: int = 0
    grid_size: int = 128
    walls: set[tuple[int, int, str]] = field(default_factory=set)

    # ------------------------------------------------------------------ #
    # Event handlers – call these from your main loop when an event arrives
    # ------------------------------------------------------------------ #

    def handle_player_joined(self, tank_id: str) -> None:
        if tank_id not in self.tanks:
            self.tanks[tank_id] = TankState(tank_id=tank_id)

    def handle_map(self, data: dict) -> None:
        self.game_over = False
        self.game_started = False
        self.tanks.clear()

        self.grid_width = data["gridWidth"]
        self.grid_height = data["gridHeight"]
        self.grid_size = data["gridSize"]
        self.walls = {
            (w["x"], w["y"], w["orientation"]) for w in data["walls"]
        }
        for p in data["players"]:
            tid = p["tankId"]
            if tid not in self.tanks:
                self.tanks[tid] = TankState(tank_id=tid)
            self.tanks[tid].grid_x = p["x"]
            self.tanks[tid].grid_y = p["y"]

    def handle_game_started(self, on_turn: str) -> None:
        self.game_started = True
        self.on_turn = on_turn

    def handle_turn_changed(self, next_turn: str) -> None:
        self.on_turn = next_turn

    def handle_moved(self, tank_id: str, direction: str, pos_x: float, pos_y: float) -> None:
        if tank_id in self.tanks:
            tank = self.tanks[tank_id]
            tank.pos_x = pos_x
            tank.pos_y = pos_y
            tank.grid_x = int(pos_x) // self.grid_size
            tank.grid_y = int(pos_y) // self.grid_size

    def handle_rotated(self, tank_id: str, degrees: float) -> None:
        if tank_id in self.tanks:
            self.tanks[tank_id].turret_degrees = degrees

    def handle_game_over(self, winner: str) -> None:
        self.game_over = True
        self.winner = winner

    def handle_turn_timeout(self, tank_id: str) -> None:
        pass  # Turn skipped; server will send turn_changed next

    def handle_turn_disqualified(self, tank_id: str) -> None:
        if tank_id in self.tanks:
            self.tanks[tank_id].alive = False

    # ------------------------------------------------------------------ #
    # Convenience helpers
    # ------------------------------------------------------------------ #

    @property
    def is_my_turn(self) -> bool:
        return self.game_started and self.on_turn == self.my_tank_id

    def my_tank(self) -> TankState | None:
        return self.tanks.get(self.my_tank_id)

    def opponents(self) -> list[TankState]:
        return [t for tid, t in self.tanks.items() if tid != self.my_tank_id and t.alive]

    def has_wall(self, x: int, y: int, orientation: str) -> bool:
        """Check if a wall exists at (x, y) with the given orientation."""
        return (x, y, orientation) in self.walls
