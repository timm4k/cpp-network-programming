#include "CurrencyClientService.h"

#include <chrono>
#include <stdexcept>

CurrencyClientService::CurrencyClientService(network::ClientEndpoint endpoint)
    : connection_(network::connect_with_retry(endpoint, std::chrono::milliseconds(500)))
{
}

protocol::ServerResponse CurrencyClientService::opening_response()
{
    if (openingResponseRead_)
    {
        throw std::logic_error("Opening response has already been read");
    }
    openingResponseRead_ = true;
    return receive_response();
}

protocol::ServerResponse CurrencyClientService::authenticate(const protocol::Credentials& credentials)
{
    if (!openingResponseRead_)
    {
        throw std::logic_error("Opening response must be read before authentication");
    }
    connection_.send_line(protocol::encode_auth_request(credentials));
    return receive_response();
}

protocol::ServerResponse CurrencyClientService::request_rate(const protocol::CurrencyPair& pair)
{
    connection_.send_line(protocol::encode_rate_request(pair));
    return receive_response();
}

protocol::ServerResponse CurrencyClientService::disconnect()
{
    connection_.send_line(protocol::ExitCommand);
    return receive_response();
}

protocol::ServerResponse CurrencyClientService::receive_response()
{
    const std::optional<std::string> message = connection_.receive_line();
    if (!message)
    {
        throw std::runtime_error("Server closed the connection without a response");
    }
    return protocol::decode_server_response(*message);
}