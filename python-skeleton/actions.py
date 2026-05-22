from enum import Enum

class DIRECTIONS(Enum):
    UP = 'up'
    DOWN = 'down'
    LEFT = 'left'
    RIGHT = 'right'

def ActionRotation(degrees: int) -> dict[str, str| int]:
    return {"type": "rotate", "degrees": degrees}

def ActionMove(direction: DIRECTIONS) -> dict[str, str| int]:
    return {"type": "move", "direction": str(direction)}
    
def ActionShoot() -> dict[str, str| int]:
    return {"type": "shoot"}