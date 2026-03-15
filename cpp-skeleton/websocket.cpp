#include "websocket.hpp"

#include <asio.hpp>

#include <cstdint>
#include <cstdlib>
#include <istream>
#include <stdexcept>
#include <string>
#include <vector>

namespace ws {

std::vector<uint8_t> make_text_frame(const std::string &payload) {
  std::vector<uint8_t> frame;
  frame.push_back(0x81); // FIN=1, opcode=text

  uint8_t mask_key[4];
  for (int i = 0; i < 4; ++i)
    mask_key[i] = static_cast<uint8_t>(std::rand() & 0xFF);

  const size_t len = payload.size();
  if (len < 126) {
    frame.push_back(0x80 | static_cast<uint8_t>(len));
  } else if (len < 65536) {
    frame.push_back(0x80 | 126);
    frame.push_back(static_cast<uint8_t>(len >> 8));
    frame.push_back(static_cast<uint8_t>(len & 0xFF));
  } else {
    frame.push_back(0x80 | 127);
    for (int i = 7; i >= 0; --i)
      frame.push_back(static_cast<uint8_t>(len >> (8 * i)));
  }

  frame.insert(frame.end(), mask_key, mask_key + 4);
  for (size_t i = 0; i < len; ++i)
    frame.push_back(static_cast<uint8_t>(payload[i]) ^ mask_key[i % 4]);

  return frame;
}

std::string read_frame(asio::ip::tcp::socket &sock) {
  while (true) {
    uint8_t hdr[2];
    asio::read(sock, asio::buffer(hdr, 2));

    const uint8_t opcode = hdr[0] & 0x0F;
    const bool masked = (hdr[1] & 0x80) != 0;
    uint64_t len = hdr[1] & 0x7F;

    if (len == 126) {
      uint8_t ext[2];
      asio::read(sock, asio::buffer(ext, 2));
      len = (static_cast<uint64_t>(ext[0]) << 8) | ext[1];
    } else if (len == 127) {
      uint8_t ext[8];
      asio::read(sock, asio::buffer(ext, 8));
      len = 0;
      for (int i = 0; i < 8; ++i)
        len = (len << 8) | ext[i];
    }

    uint8_t mask_key[4] = {};
    if (masked)
      asio::read(sock, asio::buffer(mask_key, 4));

    std::vector<uint8_t> payload(static_cast<size_t>(len));
    if (len > 0)
      asio::read(sock, asio::buffer(payload));

    if (masked)
      for (size_t i = 0; i < payload.size(); ++i)
        payload[i] ^= mask_key[i % 4];

    if (opcode == 0x1 || opcode == 0x0) { // text / continuation
      return std::string(payload.begin(), payload.end());
    } else if (opcode == 0x9) { // ping → pong
      uint8_t pong_hdr[2] = {0x8A, static_cast<uint8_t>(payload.size())};
      asio::write(sock, asio::buffer(pong_hdr, 2));
      if (!payload.empty())
        asio::write(sock, asio::buffer(payload));
    } else if (opcode == 0x8) { // close
      throw std::runtime_error("server closed the connection");
    }
  }
}

asio::ip::tcp::socket connect(asio::io_context &io, const std::string &host,
                              const std::string &port) {
  asio::ip::tcp::resolver resolver(io);
  asio::ip::tcp::socket sock(io);
  asio::connect(sock, resolver.resolve(host, port));

  const std::string request =
      "GET / HTTP/1.1\r\n"
      "Host: " + host + ":" + port + "\r\n"
      "Upgrade: websocket\r\n"
      "Connection: Upgrade\r\n"
      "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n"
      "Sec-WebSocket-Version: 13\r\n"
      "\r\n";

  asio::write(sock, asio::buffer(request));

  asio::streambuf response;
  asio::read_until(sock, response, "\r\n\r\n");

  std::istream resp_stream(&response);
  std::string status_line;
  std::getline(resp_stream, status_line);

  if (status_line.find("101") == std::string::npos)
    throw std::runtime_error("WebSocket upgrade failed: " + status_line);

  return sock;
}

} // namespace ws
