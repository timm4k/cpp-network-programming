#pragma once

#include "SocketSupport.h"

#include <cstdint>
#include <string>
#include <string_view>

namespace application
{
    inline constexpr std::uint16_t GreetingPort = 5051;
    inline constexpr std::uint16_t DateTimePort = 5052;
    inline constexpr std::string_view GreetingRequest = "Hello, server";
    inline constexpr std::string_view GreetingResponse = "Hello, client";
    inline constexpr std::string_view DateCommand = "DATE";
    inline constexpr std::string_view TimeCommand = "TIME";
    inline constexpr std::string_view ExitCommand = "EXIT";

    void configure_console();
    std::string current_time();
    std::string current_date();
    std::string read_date_time_command();
    std::string make_date_time_response(std::string_view request);
    void print_received(std::string_view endpoint, std::string_view message);
}