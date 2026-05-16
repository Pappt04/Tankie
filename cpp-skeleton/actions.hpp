#pragma once

#include <array>
#include <cstdlib>
#include <string>
#include <vector>

namespace actions {

enum ACTION_DIRECTIONS { UP = 0, DOWN = 1, LEFT = 2, RIGHT = 3 };

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
  std::vector<std::string> actions;

public:
  ActionList() { actions.resize(3); }
  void addShoot() { actions.push_back(shoot()); }

  void addMove(const ACTION_DIRECTIONS direction) {
    actions.push_back(move(directions[direction]));
  }

  void addRotate(int degrees) { actions.push_back(rotate(degrees)); }

  std::string getJsonString() {

    std::string json = "[";
    for (auto it = actions.begin(); it != actions.end(); it++) {
      json += *it;
      if (it + 1 != actions.end())
        json += ",";
    }
    json += "]";
    return json;
  }
};

} // namespace actions
