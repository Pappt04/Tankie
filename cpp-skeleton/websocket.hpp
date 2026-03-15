#pragma once

#include <asio.hpp>

#include <cstdint>
#include <string>
#include <vector>

// WebSocket framing utilities (RFC 6455, text frames only, no TLS).
namespace ws {

std::vector<uint8_t> make_text_frame(const std::string &payload);
std::string read_frame(asio::ip::tcp::socket &sock);
asio::ip::tcp::socket connect(asio::io_context &io, const std::string &host,
                              const std::string &port);

} // namespace ws
