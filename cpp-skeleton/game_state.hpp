#pragma once

#include "tank_state.hpp"

#include <set>
#include <string>
#include <tuple>
#include <unordered_map>
#include <vector>

// ------------------------------------------------------------------ //
// Wall – a single wall entry from the map event
// ------------------------------------------------------------------ //
struct Wall {
  int x;
  int y;
  std::string orientation; // "HORIZONTAL" or "VERTICAL"
};

// ------------------------------------------------------------------ //
// GameState – local mirror, updated from incoming server events
// ------------------------------------------------------------------ //
class GameState {
public:
  explicit GameState(std::string my_tank_id)
      : my_tank_id_(std::move(my_tank_id)) {}

  // ---- event handlers ------------------------------------------ //

  void handle_player_joined(const std::string &tank_id) {
    if (tanks_.find(tank_id) == tanks_.end())
      tanks_[tank_id] = TankState{tank_id};
  }

  // Called with a vector of Wall entries and player spawns parsed from
  // the "map" event. See client.cpp dispatch() for how it is populated.
  void handle_map(int grid_width, int grid_height, int grid_size,
                  const std::vector<Wall> &walls,
                  const std::vector<std::pair<std::string, std::pair<int,int>>> &players) {
    game_over_    = false;
    game_started_ = false;
    tanks_.clear();

    grid_width_  = grid_width;
    grid_height_ = grid_height;
    grid_size_   = grid_size;

    walls_.clear();
    for (const auto &w : walls)
      walls_.insert({w.x, w.y, w.orientation});

    for (const auto &[tank_id, pos] : players) {
      if (tanks_.find(tank_id) == tanks_.end())
        tanks_[tank_id] = TankState{tank_id};
      tanks_[tank_id].grid_x = pos.first;
      tanks_[tank_id].grid_y = pos.second;
    }
  }

  void handle_game_started(const std::string &on_turn) {
    game_started_ = true;
    on_turn_ = on_turn;
  }

  void handle_turn_changed(const std::string &next_turn) {
    on_turn_ = next_turn;
  }

  void handle_moved(const std::string &tank_id,
                    const std::string & /*direction*/, float pos_x,
                    float pos_y) {
    auto it = tanks_.find(tank_id);
    if (it != tanks_.end()) {
      it->second.pos_x  = pos_x;
      it->second.pos_y  = pos_y;
      it->second.grid_x = static_cast<int>(pos_x) / grid_size_;
      it->second.grid_y = static_cast<int>(pos_y) / grid_size_;
    }
  }

  void handle_rotated(const std::string &tank_id, float degrees) {
    auto it = tanks_.find(tank_id);
    if (it != tanks_.end())
      it->second.turret_degrees = degrees;
  }

  void handle_game_over(const std::string &winner) {
    game_over_ = true;
    winner_ = winner;
  }

  void handle_turn_disqualified(const std::string &tank_id) {
    auto it = tanks_.find(tank_id);
    if (it != tanks_.end())
      it->second.alive = false;
  }

  // ---- convenience helpers ------------------------------------- //

  bool is_my_turn() const { return game_started_ && on_turn_ == my_tank_id_; }

  const TankState *my_tank() const {
    auto it = tanks_.find(my_tank_id_);
    return it != tanks_.end() ? &it->second : nullptr;
  }

  std::vector<const TankState *> opponents() const {
    std::vector<const TankState *> result;
    for (const auto &[id, tank] : tanks_)
      if (id != my_tank_id_ && tank.alive)
        result.push_back(&tank);
    return result;
  }

  /// Check if a wall exists at (x, y) with the given orientation.
  bool has_wall(int x, int y, const std::string &orientation) const {
    return walls_.count({x, y, orientation}) > 0;
  }

  // ---- accessors ----------------------------------------------- //

  const std::string &my_tank_id() const { return my_tank_id_; }
  const std::string &on_turn() const { return on_turn_; }
  bool game_started() const { return game_started_; }
  bool game_over() const { return game_over_; }
  const std::string &winner() const { return winner_; }
  int grid_width()  const { return grid_width_; }
  int grid_height() const { return grid_height_; }
  int grid_size()   const { return grid_size_; }

private:
  std::string my_tank_id_;
  std::unordered_map<std::string, TankState> tanks_;

  std::string on_turn_;
  bool game_started_ = false;
  bool game_over_    = false;
  std::string winner_;

  // Map state
  int grid_width_  = 0;
  int grid_height_ = 0;
  int grid_size_   = 128;
  std::set<std::tuple<int, int, std::string>> walls_;
};
