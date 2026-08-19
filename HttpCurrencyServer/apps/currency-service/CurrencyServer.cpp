#include "ApplicationSupport.h"
#include "CurrencyProtocol.h"
#include "CurrencyServerService.h"
#include "SocketTransport.h"

#include <charconv>
#include <format>
#include <iostream>
#include <stdexcept>
#include <string>
#include <unordered_map>
#include <utility>

namespace
{
    std::size_t read_number(
        std::string_view prompt,
        std::size_t minimum,
        std::size_t maximum,
        std::size_t defaultValue)
    {
        while (true)
        {
            std::cout << prompt << " [" << minimum << '-' << maximum
                << ", default: " << defaultValue << ", Enter = default]: ";
            std::string input;
            if (!std::getline(std::cin, input))
            {
                throw std::runtime_error("Console input was closed");
            }
            input = application::trim(input);
            if (input.empty())
            {
                return defaultValue;
            }

            unsigned long long value = 0;
            const auto [end, error] = std::from_chars(input.data(), input.data() + input.size(), value);
            if (error == std::errc{} && end == input.data() + input.size() && value >= minimum && value <= maximum)
            {
                return static_cast<std::size_t>(value);
            }
            std::cout << "Enter a whole number from " << minimum << " to " << maximum << '\n';
        }
    }

    std::unordered_map<std::string, std::string> read_credentials()
    {
        std::unordered_map<std::string, std::string> credentials;
        std::cout << "Add user accounts. Leave username empty after at least one account to start the server\n";

        while (true)
        {
            std::cout << "Username: ";
            std::string username;
            if (!std::getline(std::cin, username))
            {
                throw std::runtime_error("Console input was closed");
            }
            username = application::trim(username);
            if (username.empty())
            {
                if (!credentials.empty())
                {
                    return credentials;
                }
                std::cout << "At least one account is required\n";
                continue;
            }

            std::cout << "Password: ";
            std::string password;
            if (!std::getline(std::cin, password))
            {
                throw std::runtime_error("Console input was closed");
            }
            password = application::trim(password);

            try
            {
                protocol::encode_auth_request({ username, password });
            }
            catch (const std::invalid_argument& error)
            {
                std::cout << "Error: " << error.what() << '\n';
                continue;
            }

            credentials[std::move(username)] = std::move(password);
        }
    }
}

int main(int argc, char* argv[])
{
    try
    {
        application::configure_console();
        CurrencyServerConfiguration configuration;
        configuration.port = network::parse_server_port(argc, argv, protocol::DefaultPort);
        configuration.reconnectDelay = protocol::ReconnectDelay;
        const std::string requestPrompt = std::format(
            "Requests per session before a {}-second cooldown", configuration.reconnectDelay.count());
        configuration.maxRequestsPerSession = read_number(
            requestPrompt, 1, 1000, configuration.maxRequestsPerSession);
        configuration.maxConnectedClients = read_number(
            "Simultaneous connected clients", 1, 100, configuration.maxConnectedClients);
        configuration.credentials = read_credentials();

        CurrencyServerService server(std::move(configuration), [](std::string_view message)
        {
            std::cout << message << '\n';
        });
        server.run();
    }
    catch (const std::exception& error)
    {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}
