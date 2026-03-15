#include "json_parser.hpp"

std::string JsonParser::extractString(const std::string &json,
                                      const std::string &key) {
  std::string needle = "\"" + key + "\"";
  auto pos = json.find(needle);
  if (pos == std::string::npos)
    return "";
  pos = json.find(':', pos + needle.size());
  if (pos == std::string::npos)
    return "";
  pos = json.find('"', pos + 1);
  if (pos == std::string::npos)
    return "";
  auto end = json.find('"', pos + 1);
  if (end == std::string::npos)
    return "";
  return json.substr(pos + 1, end - pos - 1);
}

float JsonParser::extractFloat(const std::string &json,
                               const std::string &key) {
  std::string needle = "\"" + key + "\"";
  auto pos = json.find(needle);
  if (pos == std::string::npos)
    return 0.f;
  pos = json.find(':', pos + needle.size());
  if (pos == std::string::npos)
    return 0.f;
  ++pos;
  while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t'))
    ++pos;
  return std::stof(json.substr(pos));
}

int JsonParser::extractInt(const std::string &json, const std::string &key) {
  return static_cast<int>(extractFloat(json, key));
}

std::vector<std::string>
JsonParser::extractObjectArray(const std::string &json,
                               const std::string &key) {
  std::string needle = "\"" + key + "\"";
  auto pos = json.find(needle);
  if (pos == std::string::npos)
    return {};
  pos = json.find('[', pos + needle.size());
  if (pos == std::string::npos)
    return {};

  std::vector<std::string> result;
  size_t i = pos + 1;
  while (i < json.size()) {
    while (i < json.size() &&
           (json[i] == ' ' || json[i] == ',' || json[i] == '\n' ||
            json[i] == '\r' || json[i] == '\t'))
      ++i;
    if (i >= json.size() || json[i] == ']')
      break;
    if (json[i] == '{') {
      int depth = 0;
      size_t start = i;
      while (i < json.size()) {
        if (json[i] == '{')
          ++depth;
        else if (json[i] == '}') {
          --depth;
          if (depth == 0) {
            ++i;
            break;
          }
        }
        ++i;
      }
      result.push_back(json.substr(start, i - start));
    } else {
      break;
    }
  }
  return result;
}

std::string JsonParser::make_join(const std::string &tank_id) {
  return R"({"action":"join","tankId":")" + tank_id + R"("})";
}

std::string JsonParser::make_command(const std::string &tank_id,
                                     const std::string &actions_array) {
  return R"({"tankId":")" + tank_id + R"(","actions":)" + actions_array + "}";
}
