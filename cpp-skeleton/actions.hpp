#pragma once

#include <array>
#include <cstdlib>
#include <string>

namespace actions {

enum DIRECTIONS { UP, DOWN, LEFT, RIGHT };

static const std::array<std::string, 4> directions = {"up", "down", "left",
                                                      "right"};
inline std::string move(const std::string &direction) {
  return R"({"type":"move","direction":")" + direction + R"("})";
}

inline std::string rotate(int degrees) {
  return R"({"type":"rotate","degrees":)" + std::to_string(degrees) + "}";
}

inline std::string shoot() { return R"({"type":"shoot"})"; }

class ActionList {

private:
  std::string action;

public:
  ActionList() { action += "["; }

  void addShoot() { action += shoot() + ","; }

  void addMove(const DIRECTIONS direction) {
    std::string t = move(directions[direction]);
    action += t + ",";
  }

  void addRotate(int degrees) { action += rotate(degrees); }

  std::string jsonString() {}
};

} // namespace actions
