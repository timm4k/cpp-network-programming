#pragma once

#include <WinSock2.h>
#include <WS2tcpip.h>

#include <cstddef>
#include <chrono>
#include <cstdint>
#include <optional>
#include <string>
#include <string_view>

namespace network
{
    inline constexpr std::size_t MaxLineSize = 1024;

    class WinsockRuntime
    {
    public:
        WinsockRuntime();
        ~WinsockRuntime();

        WinsockRuntime(const WinsockRuntime&) = delete;
        WinsockRuntime& operator=(const WinsockRuntime&) = delete;
    };

    class Socket
    {
    public:
        Socket() noexcept = default;
        explicit Socket(SOCKET handle) noexcept;
        ~Socket();

        Socket(const Socket&) = delete;
        Socket& operator=(const Socket&) = delete;
        Socket(Socket&& other) noexcept;
        Socket& operator=(Socket&& other) noexcept;

        [[nodiscard]] SOCKET get() const noexcept;
        [[nodiscard]] bool valid() const noexcept;

    private:
        void close() noexcept;

        SOCKET handle_ = INVALID_SOCKET;
    };

    struct ClientEndpoint
    {
        std::string host = "127.0.0.1";
        std::uint16_t port = 0;
    };

    class TcpConnection
    {
    public:
        explicit TcpConnection(Socket socket);

        TcpConnection(const TcpConnection&) = delete;
        TcpConnection& operator=(const TcpConnection&) = delete;
        TcpConnection(TcpConnection&&) noexcept = default;
        TcpConnection& operator=(TcpConnection&&) noexcept = default;

        void send_line(std::string_view message) const;
        [[nodiscard]] std::optional<std::string> receive_line();
        [[nodiscard]] std::string remote_endpoint() const;

    private:
        Socket socket_;
        std::string pending_;
    };

    class TcpListener
    {
    public:
        explicit TcpListener(std::uint16_t port);
        [[nodiscard]] TcpConnection accept() const;

    private:
        Socket socket_;
    };

    std::uint16_t parse_server_port(int argc, char* argv[], std::uint16_t defaultPort);
    ClientEndpoint parse_client_endpoint(int argc, char* argv[], std::uint16_t defaultPort);
    TcpConnection connect_with_retry(const ClientEndpoint& endpoint, std::chrono::milliseconds retryDelay);
}
