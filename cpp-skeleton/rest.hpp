#pragma once

/*
 * REST helpers – blocking HTTP GET for the Tankie server endpoints.
 *
 * Call sparingly (e.g. once at startup for constants, or between turns),
 * not on every frame, as each call opens a new TCP connection.
 *
 * Usage:
 *   std::string body = rest::get("localhost", "8080", "/constants");
 *   float speed = JsonParser::extractFloat(body, "bulletSpeed");
 *
 * Convenience wrappers (defined in main.cpp after HOST/PORT are known):
 *   fetch_map()           – wall list + gridSize
 *   fetch_players()       – all living players
 *   fetch_player(tankId)  – single player (throws if 404)
 *   fetch_state()         – gameStarted, onTurn, round, scores
 *   fetch_constants()     – bullet physics constants
 */

#include <asio.hpp>
#include <stdexcept>
#include <string>

namespace rest {

// Blocking HTTP/1.0 GET. Returns the response body.
// Throws std::runtime_error on network error or non-200 status.
inline std::string get(const std::string &host, const std::string &port,
                       const std::string &path) {
  asio::io_context io;
  asio::ip::tcp::resolver resolver(io);
  auto endpoints = resolver.resolve(host, port);

  asio::ip::tcp::socket sock(io);
  asio::connect(sock, endpoints);

  // HTTP/1.0 request so the server closes the connection after the response,
  // giving us a clean EOF to read until.
  const std::string request =
      "GET " + path +
      " HTTP/1.0\r\n"
      "Host: " +
      host + ":" + port +
      "\r\n"
      "Connection: close\r\n"
      "\r\n";
  asio::write(sock, asio::buffer(request));

  // Read until EOF.
  asio::streambuf buf;
  asio::error_code ec;
  asio::read(sock, buf, ec);
  if (ec && ec != asio::error::eof)
    throw std::runtime_error("HTTP read error: " + ec.message());

  const std::string raw{asio::buffers_begin(buf.data()),
                        asio::buffers_end(buf.data())};

  // Verify 200 OK.
  if (raw.find(" 200 ") == std::string::npos)
    throw std::runtime_error("HTTP error: " + raw.substr(0, raw.find('\r')));

  // Strip headers – body starts after the first blank line.
  const auto sep = raw.find("\r\n\r\n");
  if (sep == std::string::npos)
    throw std::runtime_error("Malformed HTTP response");

  return raw.substr(sep + 4);
}

} // namespace rest
