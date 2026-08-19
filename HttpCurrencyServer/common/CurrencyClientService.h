#pragma once

#include "CurrencyProtocol.h"
#include "SocketTransport.h"

class CurrencyClientService
{
public:
    explicit CurrencyClientService(network::ClientEndpoint endpoint);

    CurrencyClientService(const CurrencyClientService&) = delete;
    CurrencyClientService& operator=(const CurrencyClientService&) = delete;

    protocol::ServerResponse opening_response();
    protocol::ServerResponse authenticate(const protocol::Credentials& credentials);
    protocol::ServerResponse request_rate(const protocol::CurrencyPair& pair);
    protocol::ServerResponse disconnect();

private:
    protocol::ServerResponse receive_response();

    network::WinsockRuntime winsock_;
    network::TcpConnection connection_;
    bool openingResponseRead_ = false;
};