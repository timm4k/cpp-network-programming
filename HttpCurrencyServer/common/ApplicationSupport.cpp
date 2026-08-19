#include "ApplicationSupport.h"

#include <Windows.h>

#include <algorithm>
#include <chrono>
#include <cctype>
#include <format>

namespace application
{
    void configure_console()
    {
        SetConsoleCP(CP_UTF8);
        SetConsoleOutputCP(CP_UTF8);
    }

    std::string current_timestamp()
    {
        const auto now = std::chrono::floor<std::chrono::seconds>(std::chrono::system_clock::now());
        return std::format("{:%Y-%m-%d %H:%M:%S}", now);
    }

    std::string trim(std::string_view value)
    {
        const auto isNotSpace = [](unsigned char character)
        {
            return std::isspace(character) == 0;
        };

        const auto first = std::find_if(value.begin(), value.end(), isNotSpace);
        const auto last = std::find_if(value.rbegin(), value.rend(), isNotSpace).base();
        return first < last ? std::string(first, last) : std::string{};
    }

    std::string uppercase(std::string value)
    {
        std::ranges::transform(value, value.begin(), [](unsigned char character)
        {
            return static_cast<char>(std::toupper(character));
        });
        return value;
    }
}
