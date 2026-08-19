#include "ApplicationSupport.h"

#include <Windows.h>

#include <algorithm>
#include <chrono>
#include <cctype>
#include <iomanip>
#include <iostream>
#include <sstream>

namespace
{
    std::tm local_time()
    {
        const std::time_t value = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
        std::tm result{};
        localtime_s(&result, &value);
        return result;
    }
}

namespace application
{
    void configure_console()
    {
        SetConsoleCP(CP_UTF8);
        SetConsoleOutputCP(CP_UTF8);
    }

    std::string current_time()
    {
        const std::tm time = local_time();
        std::ostringstream output;
        output << std::put_time(&time, "%H:%M:%S");
        return output.str();
    }

    std::string current_date()
    {
        const std::tm time = local_time();
        std::ostringstream output;
        output << std::put_time(&time, "%Y-%m-%d");
        return output.str();
    }

    std::string read_date_time_command()
    {
        while (true)
        {
            std::cout << "Enter DATE, TIME, or EXIT: ";
            std::string request;
            if (!std::getline(std::cin, request))
            {
                return std::string(ExitCommand);
            }
            std::transform(request.begin(), request.end(), request.begin(), [](unsigned char character)
            {
                return static_cast<char>(std::toupper(character));
            });
            if (request == DateCommand || request == TimeCommand || request == ExitCommand)
            {
                return request;
            }
            std::cout << "Invalid command. Enter DATE, TIME, or EXIT\n";
        }
    }

    std::string make_date_time_response(std::string_view request)
    {
        if (request == DateCommand)
        {
            return current_date();
        }
        if (request == TimeCommand)
        {
            return current_time();
        }
        return "ERROR: unknown request";
    }

    void print_received(std::string_view endpoint, std::string_view message)
    {
        std::cout << "At " << current_time().substr(0, 5) << " from [" << endpoint << "] received: " << message << '\n';
    }
}