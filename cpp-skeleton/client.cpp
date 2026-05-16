/*
 * Tankie – C++ WebSocket client skeleton
 *
 * Build:  cmake -B build && cmake --build build
 * Run:    ./build/tankie_client
 *
 * Make sure the Godot game is running and showing the Menu screen before
 * connecting.  Press "Start" in-game once all players have joined.
 *
 * NOTE: websocketpp-0.8.2 is not compatible with asio-1.36.0 (it relies on
 * io_service, resolver::iterator, strand::wrap, io_service::work and
 * expires_from_now, all of which were removed in asio 1.12+).
 * This file therefore implements the WebSocket protocol directly on top of
 * standalone Asio – the framing code is ~60 lines and fits in one file.
 */

#include "client.hpp"
#include "json_parser.hpp"
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <string>

// ------------------------------------------------------------------ //
// Event dispatcher
// ------------------------------------------------------------------ //
void dispatch(const std::string &json, GameState &state) {
  const std::string event = JsonParser::extractString(json, "event");

  if (event == "player_joined") {
    const std::string tank_id = JsonParser::extractString(json, "tankId");
    state.handle_player_joined(tank_id);
    std::cout << "[lobby] " << tank_id << " joined\n";

  } else if (event == "map") {
    const int grid_width  = JsonParser::extractInt(json, "gridWidth");
    const int grid_height = JsonParser::extractInt(json, "gridHeight");
    const int grid_size   = JsonParser::extractInt(json, "gridSize");

    std::vector<Wall> walls;
    for (const auto &obj : JsonParser::extractObjectArray(json, "walls")) {
      Wall w;
      w.x           = JsonParser::extractInt(obj, "x");
      w.y           = JsonParser::extractInt(obj, "y");
      w.orientation = JsonParser::extractString(obj, "orientation");
      walls.push_back(std::move(w));
    }

    std::vector<std::pair<std::string, std::pair<int, int>>> players;
    for (const auto &obj : JsonParser::extractObjectArray(json, "players")) {
      const std::string tid = JsonParser::extractString(obj, "tankId");
      const int x = JsonParser::extractInt(obj, "x");
      const int y = JsonParser::extractInt(obj, "y");
      players.push_back({tid, {x, y}});
    }

    state.handle_map(grid_width, grid_height, grid_size, walls, players);
    std::cout << "[map] received " << walls.size() << " walls, grid "
              << grid_width << "x" << grid_height << "\n";

  } else if (event == "game_started") {
    const std::string on_turn = JsonParser::extractString(json, "onTurn");
    state.handle_game_started(on_turn);
    std::cout << "[game] started – first turn: " << on_turn << "\n";

  } else if (event == "turn_changed") {
    const std::string next_turn = JsonParser::extractString(json, "nextTurn");
    state.handle_turn_changed(next_turn);
    std::cout << "[game] turn -> " << next_turn << "\n";

  } else if (event == "moved") {
    const std::string tank_id = JsonParser::extractString(json, "tankId");
    const std::string direction = JsonParser::extractString(json, "direction");
    const float pos_x = JsonParser::extractFloat(json, "pos_x");
    const float pos_y = JsonParser::extractFloat(json, "pos_y");
    state.handle_moved(tank_id, direction, pos_x, pos_y);
    std::cout << "[move] " << tank_id << " -> " << direction << " (" << pos_x
              << ", " << pos_y << ")\n";

  } else if (event == "rotated") {
    const std::string tank_id = JsonParser::extractString(json, "tankId");
    const float degrees = JsonParser::extractFloat(json, "degrees");
    state.handle_rotated(tank_id, degrees);
    std::cout << "[rotate] " << tank_id << " turret -> " << degrees << " deg\n";

  } else if (event == "shot") {
    std::cout << "[shot] " << JsonParser::extractString(json, "tankId")
              << " fired\n";

  } else if (event == "turn_timeout") {
    const std::string tank_id = JsonParser::extractString(json, "tankId");
    std::cout << "[timeout] " << tank_id << " timed out — turn skipped (1st warning)\n";

  } else if (event == "turn_disqualified") {
    const std::string tank_id = JsonParser::extractString(json, "tankId");
    state.handle_turn_disqualified(tank_id);
    std::cout << "[disqualified] " << tank_id << " timed out twice — eliminated\n";

  } else if (event == "game_over") {
    const std::string winner = JsonParser::extractString(json, "winner");
    state.handle_game_over(winner);
    const int round = JsonParser::extractInt(json, "round");
    if (winner.empty())
      std::cout << "[game] Draw! Round " << round << " | Waiting for next round...\n";
    else
      std::cout << "[game] " << winner << " wins! Round " << round << " | Waiting for next round...\n";

  } else if (event == "lobby_reset") {
    std::cout << "[lobby] Server reset lobby — reconnecting...\n";

  } else {
    std::cout << "[unknown event] " << json << "\n";
  }
}
