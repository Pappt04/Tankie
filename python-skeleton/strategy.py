"""
Tankie – strategy module.

Implement decide_actions() to control your tank each turn.

Action budget per turn:
  - Up to 2 MOVE actions, OR
  - 1 MOVE + 1 SHOOT, OR
  - 1 SHOOT only
  - Up to 1 ROTATE action (free – no budget cost)

Action JSON shapes:
  {"type": "move",   "direction": "up"|"down"|"left"|"right"}
  {"type": "rotate", "degrees": <0-359>}
  {"type": "shoot"}
"""

import random
from actions import ActionMove, ActionRotation, ActionShoot
from game_state import GameState
from enum import Enum

class DIRECTIONS(Enum):
    UP = 'up'
    DOWN = 'down'
    LEFT = 'left'
    RIGHT = 'right'

def decide_actions(state: GameState) -> list[dict]:
    """
    Return a list of actions for this turn.

    The list is sent as-is to the server inside the 'actions' field.
    Respect the action budget or the server will reject the command.

    Parameters
    ----------
    state : GameState
        Current snapshot of the game (positions, whose turn it is, etc.)

    Returns
    -------
    list[dict]
        Ordered list of action dicts to execute this turn.
    """
    # TODO: replace the random strategy below with your own logic
    actions: list[dict] = []

    # Example: rotate to a random angle (free action – no budget cost)
    actions.append(ActionRotation(random.randint(0, 359)))

    # Example: move in a random direction (costs 1 budget)
    actions.append(ActionMove(DIRECTIONS.LEFT))

    # Example: shoot (costs 1 budget – together with the move above this
    #          exhausts the 2-unit budget, so no more moves after this)
    actions.append(ActionShoot())

    return actions
