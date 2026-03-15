#pragma once

/*
 * Strategy module – implement decide_actions() to control your tank.
 *
 * Action budget per turn:
 *   - Up to 2 MOVE actions, OR
 *   - 1 MOVE + 1 SHOOT, OR
 *   - 1 SHOOT only
 *   - Any number of ROTATE actions (free – no budget cost)
 *
 * Return a JSON array string, e.g.:
 *   [{"type":"rotate","degrees":90},{"type":"move","direction":"up"},{"type":"shoot"}]
 *
 * The client wraps it in: {"tankId":"…","actions":[…]}
 */

#include "game_state.hpp"

#include <cstdlib>
#include <string>
#include <array>

// ------------------------------------------------------------------ //
// Small helpers for building action JSON without a heavy dependency
// ------------------------------------------------------------------ //
namespace actions {

inline std::string move(const std::string& direction) {
    return R"({"type":"move","direction":")" + direction + R"("})";
}

inline std::string rotate(int degrees) {
    return R"({"type":"rotate","degrees":)" + std::to_string(degrees) + "}";
}

inline std::string shoot() {
    return R"({"type":"shoot"})";
}

} // namespace actions

// ------------------------------------------------------------------ //
// decide_actions – replace the body with your own strategy
// ------------------------------------------------------------------ //

/**
 * Return a JSON array string containing the actions to execute this turn.
 * Respect the budget or the server will reject the command.
 */
inline std::string decide_actions(const GameState& state) {
    // TODO: replace the random strategy below with your own logic.
    (void)state; // remove when you start using state

    static const std::array<std::string, 4> directions = {
        "up", "down", "left", "right"
    };

    int rand_degrees   = std::rand() % 360;
    const std::string& rand_dir = directions[std::rand() % directions.size()];

    // Rotate (free) + move (1 budget) + shoot (1 budget) = 2 budget units used
    std::string action_array =
        "[" +
        actions::rotate(rand_degrees) + "," +
        actions::move(rand_dir)       + "," +
        actions::shoot()              +
        "]";

    return action_array;
}
