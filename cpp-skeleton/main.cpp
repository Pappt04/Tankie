#include "client.hpp"
#include "json_parser.hpp"
#include "rest.hpp"
#include "strategy.hpp"
#include "websocket.hpp"

#include <asio.hpp>

#include <chrono>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <string>
#include <thread>

// ------------------------------------------------------------------ //
// Configuration
// ------------------------------------------------------------------ //
static const std::string HOST    = "localhost";
static const std::string PORT    = "8080";
static const std::string TANK_ID = "player1";

// ------------------------------------------------------------------ //
// REST helpers – blocking HTTP GET (call sparingly, not every turn)
//
// Each function returns the raw JSON body string.
// Parse fields with JsonParser::extractString / extractInt / extractFloat.
//
// Example:
//   auto body  = fetch_constants();
//   float speed = JsonParser::extractFloat(body, "bulletSpeed");
//   int bounces = JsonParser::extractInt(body,   "bulletMaxBounces");
// ------------------------------------------------------------------ //

/// Wall list and gridSize.
inline std::string fetch_map() { return rest::get(HOST, PORT, "/map"); }

/// All living players' positions and turret angles.
inline std::string fetch_players() { return rest::get(HOST, PORT, "/players"); }

/// Single player state. Throws if the player is not found (HTTP 404).
inline std::string fetch_player(const std::string &tank_id) {
  return rest::get(HOST, PORT, "/player/" + tank_id);
}

/// Game status: gameStarted, gameOver, onTurn, round, scores.
inline std::string fetch_state() { return rest::get(HOST, PORT, "/state"); }

/// Physics constants for bullet trajectory math:
///   gridSize, bulletSpeed, bulletMaxBounces,
///   tankBodySize, tankBodyHalfSize, muzzleOffset, bulletRadius
inline std::string fetch_constants() {
  return rest::get(HOST, PORT, "/constants");
}

// ------------------------------------------------------------------ //
// Entry point
// ------------------------------------------------------------------ //
int main() {
  std::srand(static_cast<unsigned>(std::time(nullptr)));

  std::cout << "Tankie C++ client starting...\n"
            << "Make sure the Godot game is running on the Menu screen!\n";

  while (true) {
    try {
      asio::io_context io;
      auto sock = ws::connect(io, HOST, PORT);
      std::cout << "Connected to ws://" << HOST << ":" << PORT << " as '"
                << TANK_ID << "'\n";

      // Helper: send a JSON string as a masked WebSocket text frame.
      auto send = [&](const std::string &msg) {
        auto frame = ws::make_text_frame(msg);
        asio::write(sock, asio::buffer(frame));
      };

      // Join the game.
      send(JsonParser::make_join(TANK_ID));

      GameState state(TANK_ID);

      // Main receive loop.
      while (true) {
        const std::string msg = ws::read_frame(sock);
        dispatch(msg, state);

        if (state.is_my_turn()) {
          const std::string actions = decide_actions(state);
          std::cout << "[action] sending: " << actions << "\n";
          send(JsonParser::make_command(TANK_ID, actions));
        }
      }

    } catch (const std::exception &e) {
      std::cerr << "[error] " << e.what() << " — reconnecting in 2s...\n";
    }

    std::this_thread::sleep_for(std::chrono::seconds(2));
  }

  return 0;
}
