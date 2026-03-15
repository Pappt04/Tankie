#pragma once

#include <string>

// ------------------------------------------------------------------ //
// TankState – snapshot of one tank as reported by the server
// ------------------------------------------------------------------ //
struct TankState {
  std::string tank_id;
  float pos_x = 0.f;
  float pos_y = 0.f;
  int   grid_x = 0;
  int   grid_y = 0;
  float turret_degrees = 0.f;
  bool alive = true;
};
