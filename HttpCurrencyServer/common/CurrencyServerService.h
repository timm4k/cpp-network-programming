#pragma once

#include "SocketTransport.h"

#include <chrono>
#include <cstdint>
#include <functional>
#include <memory>
#include <string>
#include <string_view>
#include <unordered_map>

struct CurrencyServerConfiguration
{
    std::uint16_t port = 0;
    std::size_t maxRequestsPerSession = 5;
    std::size_t maxConnectedClients = 10;
    std::chrono::seconds reconnectDelay{};
    std::unordered_map<std::string, std::string> credentials;
};

class CurrencyServerService
{
public:
    using LogHandler = std::function<void(std::string_view)>;

    CurrencyServerService(CurrencyServerConfiguration configuration, LogHandler logHandler);
    [[noreturn]] void run();

private:
    struct SharedState;

    static void serve_client(std::shared_ptr<SharedState> state, network::TcpConnection connection);

    std::shared_ptr<SharedState> state_;
};