#include "ApplicationSupport.h"
#include "CurrencyClientService.h"
#include "CurrencyProtocol.h"
#include "SocketTransport.h"

#include <iomanip>
#include <iostream>
#include <optional>
#include <stdexcept>
#include <string>

namespace
{
    void print_rate_response(const protocol::ServerResponse& response)
    {
        switch (response.type)
        {
        case protocol::ResponseType::Rate:
            std::cout << "1 " << response.source << " = " << std::fixed << std::setprecision(4)
                << response.rate << ' ' << response.target << '\n';
            if (response.retryAfter.count() > 0)
            {
                std::cout << "Request limit reached. Try a new session in "
                    << response.retryAfter.count() << " seconds\n";
            }
            break;
        case protocol::ResponseType::UnknownCurrency:
            std::cout << "Error: Unknown currency. Available: " << protocol::SupportedCurrencies << '\n';
            break;
        case protocol::ResponseType::InvalidRequest:
            std::cout << "Error: Invalid request\n";
            break;
        default:
            throw std::runtime_error("Server returned an unexpected rate response");
        }
    }
}

int main(int argc, char* argv[])
{
    try
    {
        application::configure_console();
        const network::ClientEndpoint endpoint = network::parse_client_endpoint(argc, argv, protocol::DefaultPort);
        std::cout << "Waiting for server on " << endpoint.host << ':' << endpoint.port << "\n";
        CurrencyClientService client(endpoint);
        std::cout << "Server connection established\n";

        const protocol::ServerResponse opening = client.opening_response();
        if (opening.type == protocol::ResponseType::ServerBusy)
        {
            std::cout << "Server is at maximum capacity. Try again later\n";
            return 0;
        }
        if (opening.type != protocol::ResponseType::Ready)
        {
            throw std::runtime_error("Server did not accept the connection");
        }

        protocol::Credentials credentials;
        std::cout << "Username: ";
        if (!std::getline(std::cin, credentials.username))
        {
            throw std::runtime_error("Console input was closed");
        }
        std::cout << "Password: ";
        if (!std::getline(std::cin, credentials.password))
        {
            throw std::runtime_error("Console input was closed");
        }

        const protocol::ServerResponse authentication = client.authenticate(credentials);
        if (authentication.type == protocol::ResponseType::InvalidCredentials)
        {
            std::cout << "Authentication failed\n";
            return 0;
        }
        if (authentication.type == protocol::ResponseType::Cooldown)
        {
            std::cout << "This account is temporarily limited. Try again in "
                << authentication.retryAfter.count() << " seconds\n";
            return 0;
        }
        if (authentication.type != protocol::ResponseType::Authenticated)
        {
            throw std::runtime_error("Server returned an unexpected authentication response");
        }

        std::cout << "Connected. Available currencies: " << protocol::SupportedCurrencies << '\n';
        while (true)
        {
            std::cout << "Enter two currencies or EXIT: ";
            std::string input;
            if (!std::getline(std::cin, input))
            {
                break;
            }

            input = application::trim(input);
            if (application::uppercase(input) == protocol::ExitCommand)
            {
                client.disconnect();
                std::cout << "Disconnected\n";
                break;
            }

            const std::optional<protocol::CurrencyPair> pair = protocol::decode_rate_request(input);
            if (!pair)
            {
                std::cout << "Enter exactly two currency codes, for example USD EURO\n";
                continue;
            }

            const protocol::ServerResponse response = client.request_rate(*pair);
            print_rate_response(response);
            if (response.retryAfter.count() > 0)
            {
                break;
            }
        }
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}
