#pragma once

#include <string>
#include <vector>

class JsonParser {
public:
  static std::string extractString(const std::string &json,
                                   const std::string &key);
  static float extractFloat(const std::string &json, const std::string &key);
  static int   extractInt(const std::string &json, const std::string &key);

  /// Returns each `{...}` object in the named array as a raw JSON string.
  static std::vector<std::string> extractObjectArray(const std::string &json,
                                                     const std::string &key);

  static std::string make_join(const std::string &tank_id);
  static std::string make_command(const std::string &tank_id,
                                  const std::string &actions_array);
};
