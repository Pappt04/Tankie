cmake_minimum_required(VERSION 3.16)
project(tankie_client CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Export compile commands so clangd (neovim LSP) can find headers and flags.
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# ------------------------------------------------------------------ #
# Header-only dependencies (bundled in this folder)
#
# NOTE: websocketpp-0.8.2 is not compatible with asio-1.36.0 because
# it relies on deprecated asio APIs (io_service, resolver::iterator,
# strand::wrap, io_service::work, expires_from_now) that were fully
# removed in asio 1.12+.  The client therefore uses raw asio directly.
# ------------------------------------------------------------------ #
set(ASIO_DIR "${CMAKE_SOURCE_DIR}/asio-1.36.0/include")

# ------------------------------------------------------------------ #
# Target
# ------------------------------------------------------------------ #
add_executable(tankie_client
    client.cpp
    json_parser.cpp
    main.cpp
    websocket.cpp
)

target_include_directories(tankie_client PRIVATE ${ASIO_DIR})

# Standalone Asio (no Boost)
target_compile_definitions(tankie_client PRIVATE ASIO_STANDALONE)

# Link pthreads (required by Asio on Linux)
find_package(Threads REQUIRED)
target_link_libraries(tankie_client PRIVATE Threads::Threads)
