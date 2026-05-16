from strategy import DIRECTIONS

def getRotation(degrees: int) -> dict[str, str| int]:
    return {"type": "rotate", "degrees": degrees}

def getMove(direction: DIRECTIONS) -> dict[str, str| int]:
    return {"type": "move", "direction": str(direction)}
    
def getShoot() -> dict[str, str| int]:
    return {"type": "shoot"}