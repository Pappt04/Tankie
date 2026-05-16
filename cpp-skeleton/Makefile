CXX      ?= g++
CXXFLAGS  = -std=c++17 -Wall -Wextra -O2 \
            -DASIO_STANDALONE \
            -I./asio-1.36.0/include
LDFLAGS   = -pthread

TARGET    = tankie_client
BUILD     = build
SRCS      = client.cpp json_parser.cpp main.cpp websocket.cpp
OBJS      = $(SRCS:%.cpp=$(BUILD)/%.o)
DEPS      = $(OBJS:.o=.d)

.PHONY: all clean

all: $(BUILD)/$(TARGET)

$(BUILD)/$(TARGET): $(OBJS)
	$(CXX) $(OBJS) $(LDFLAGS) -o $@

$(BUILD)/%.o: %.cpp | $(BUILD)
	$(CXX) $(CXXFLAGS) -MMD -MP -c $< -o $@

$(BUILD):
	mkdir -p $(BUILD)

-include $(DEPS)

clean:
	rm -rf $(BUILD)
