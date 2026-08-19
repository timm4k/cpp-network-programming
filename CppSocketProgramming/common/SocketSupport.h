#pragma once

#include <WinSock2.h>
#include <WS2tcpip.h>

#include <cstdint>
#include <string>
#include <string_view>

namespace network
{
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

    class NetworkEvent
    {
    public:
        NetworkEvent();
        ~NetworkEvent();

        NetworkEvent(const NetworkEvent&) = delete;
        NetworkEvent& operator=(const NetworkEvent&) = delete;

        [[nodiscard]] WSAEVENT get() const noexcept;

    private:
        WSAEVENT handle_ = WSA_INVALID_EVENT;
    };

    struct ClientSettings
    {
        std::string host = "127.0.0.1";
        std::uint16_t port = 0;
    };

    std::uint16_t parse_server_port(int argc, char* argv[], std::uint16_t defaultPort);
    ClientSettings parse_client_settings(int argc, char* argv[], std::uint16_t defaultPort);

    Socket create_tcp_socket();
    Socket create_listener(std::uint16_t port);
    Socket connect_sync(std::string_view host, std::uint16_t port);
    Socket accept_sync(const Socket& listener);

    void send_line(const Socket& socket, std::string_view message);
    std::string receive_line(const Socket& socket);

    void connect_async(const Socket& socket, const NetworkEvent& event, std::string_view host, std::uint16_t port);
    Socket accept_async(const Socket& listener, const NetworkEvent& event);
    void send_line_async(const Socket& socket, const NetworkEvent& event, std::string_view message);
    std::string receive_line_async(const Socket& socket, const NetworkEvent& event);

    std::string remote_endpoint(const Socket& socket);
}