#pragma once

#include <string>
#include <string_view>

namespace application
{
    void configure_console();
    std::string current_timestamp();
    std::string trim(std::string_view value);
    std::string uppercase(std::string value);
}
