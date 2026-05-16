#pragma once

/*
 * Strategy module – implement decide_actions() to control your tank.
 *
 * Action budget per turn:
 *   - Up to 2 MOVE actions, OR
 *   - 1 MOVE + 1 SHOOT, OR
 *   - 1 SHOOT only
 *   - Up to 1 ROTATE action (free – no budget cost)
 *
 * Return a JSON array string, e.g.:
 *   [{"type":"rotate","degrees":90},{"type":"move","direction":"up"},{"type":"shoot"}]
 *
 * The client wraps it in: {"tankId":"…","actions":[…]}
 */

#include "actions.hpp"
#include "game_state.hpp"
#include <array>
#include <cstdlib>
#include <string>

// ------------------------------------------------------------------ //
// decide_actions – replace the body with your own strategy
// ------------------------------------------------------------------ //
inline std::string decide_actions(const GameState &state) {
  // TODO: replace the random strategy below with your own logic.
  (void)state; // remove when you start using state

  int rand_degrees = std::rand() % 360;
  const std::string &rand_dir =
      actions::directions[std::rand() % actions::directions.size()];

  // Rotate (free) + move (1 budget) + shoot (1 budget) = 2 budget units used
  std::string action_array = "[" + actions::rotate(rand_degrees) + "," +
                             actions::move(rand_dir) + "," + actions::shoot() +
                             "]";

  return action_array;
}
