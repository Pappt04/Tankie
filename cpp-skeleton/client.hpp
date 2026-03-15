#pragma once

#include "game_state.hpp"

#include <string>

void dispatch(const std::string &json, GameState &state);
