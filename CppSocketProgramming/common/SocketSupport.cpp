#include "SocketSupport.h"

#include <array>
#include <cstddef>
#include <limits>
#include <stdexcept>
#include <utility>

namespace
{
    constexpr std::size_t MaxMessageSize = 4096;

    [[noreturn]] void throw_socket_error(std::string_view operation, int code = WSAGetLastError())
    {
        throw std::runtime_error(std::string(operation) + " failed with Winsock error " + std::to_string(code));
    }

    sockaddr_in make_address(std::string_view host, std::uint16_t port)
    {
        sockaddr_in address{};
        address.sin_family = AF_INET;
        address.sin_port = htons(port);

        const std::string hostText(host);
        if (inet_pton(AF_INET, hostText.c_str(), &address.sin_addr) != 1)
        {
            throw std::invalid_argument("Host must be a valid IPv4 address");
        }
        return address;
    }

    std::uint16_t parse_port(std::string_view text)
    {
        std::size_t parsedCharacters = 0;
        const unsigned long value = std::stoul(std::string(text), &parsedCharacters);
        if (parsedCharacters != text.size() || value == 0 || value > std::numeric_limits<std::uint16_t>::max())
        {
            throw std::invalid_argument("Port must be between 1 and 65535");
        }
        return static_cast<std::uint16_t>(value);
    }

    void wait_for_event(const network::Socket& socket, const network::NetworkEvent& event, long requestedEvent)
    {
        while (true)
        {
            const WSAEVENT eventHandle = event.get();
            if (WSAWaitForMultipleEvents(1, &eventHandle, FALSE, WSA_INFINITE, FALSE) == WSA_WAIT_FAILED)
            {
                throw_socket_error("WSAWaitForMultipleEvents");
            }

            WSANETWORKEVENTS events{};
            if (WSAEnumNetworkEvents(socket.get(), event.get(), &events) == SOCKET_ERROR)
            {
                throw_socket_error("WSAEnumNetworkEvents");
            }

            if ((events.lNetworkEvents & requestedEvent) != 0)
            {
                int index = 0;
                while (((requestedEvent >> index) & 1L) == 0)
                {
                    ++index;
                }
                if (events.iErrorCode[index] != 0)
                {
                    throw_socket_error("Asynchronous socket operation", events.iErrorCode[index]);
                }
                return;
            }

            if ((events.lNetworkEvents & FD_CLOSE) != 0)
            {
                throw std::runtime_error("Connection closed before the operation completed");
            }
        }
    }

    void append_message_chunk(std::string& message, const char* data, std::size_t size)
    {
        message.append(data, size);
        if (message.size() > MaxMessageSize)
        {
            throw std::runtime_error("Received message exceeds the allowed size");
        }
    }

    std::string finish_message(std::string message)
    {
        message.resize(message.find('\n'));
        if (!message.empty() && message.back() == '\r')
        {
            message.pop_back();
        }
        return message;
    }
}

namespace network
{
    WinsockRuntime::WinsockRuntime()
    {
        WSADATA data{};
        const int result = WSAStartup(MAKEWORD(2, 2), &data);
        if (result != 0)
        {
            throw_socket_error("WSAStartup", result);
        }
    }

    WinsockRuntime::~WinsockRuntime()
    {
        WSACleanup();
    }

    Socket::Socket(SOCKET handle) noexcept : handle_(handle)
    {
    }

    Socket::~Socket()
    {
        close();
    }

    Socket::Socket(Socket&& other) noexcept : handle_(std::exchange(other.handle_, INVALID_SOCKET))
    {
    }

    Socket& Socket::operator=(Socket&& other) noexcept
    {
        if (this != &other)
        {
            close();
            handle_ = std::exchange(other.handle_, INVALID_SOCKET);
        }
        return *this;
    }

    SOCKET Socket::get() const noexcept
    {
        return handle_;
    }

    bool Socket::valid() const noexcept
    {
        return handle_ != INVALID_SOCKET;
    }

    void Socket::close() noexcept
    {
        if (valid())
        {
            closesocket(handle_);
            handle_ = INVALID_SOCKET;
        }
    }

    NetworkEvent::NetworkEvent() : handle_(WSACreateEvent())
    {
        if (handle_ == WSA_INVALID_EVENT)
        {
            throw_socket_error("WSACreateEvent");
        }
    }

    NetworkEvent::~NetworkEvent()
    {
        if (handle_ != WSA_INVALID_EVENT)
        {
            WSACloseEvent(handle_);
        }
    }

    WSAEVENT NetworkEvent::get() const noexcept
    {
        return handle_;
    }

    std::uint16_t parse_server_port(int argc, char* argv[], std::uint16_t defaultPort)
    {
        if (argc > 2)
        {
            throw std::invalid_argument("Usage: server [port]");
        }
        return argc == 2 ? parse_port(argv[1]) : defaultPort;
    }

    ClientSettings parse_client_settings(int argc, char* argv[], std::uint16_t defaultPort)
    {
        if (argc > 3)
        {
            throw std::invalid_argument("Usage: client [host] [port]");
        }

        ClientSettings settings{ "127.0.0.1", defaultPort };
        if (argc >= 2)
        {
            settings.host = argv[1];
        }
        if (argc == 3)
        {
            settings.port = parse_port(argv[2]);
        }
        return settings;
    }

    Socket create_tcp_socket()
    {
        Socket result(socket(AF_INET, SOCK_STREAM, IPPROTO_TCP));
        if (!result.valid())
        {
            throw_socket_error("socket");
        }
        return result;
    }

    Socket create_listener(std::uint16_t port)
    {
        Socket listener = create_tcp_socket();
        const sockaddr_in address = make_address("127.0.0.1", port);
        if (bind(listener.get(), reinterpret_cast<const sockaddr*>(&address), sizeof(address)) == SOCKET_ERROR)
        {
            throw_socket_error("bind");
        }
        if (listen(listener.get(), SOMAXCONN) == SOCKET_ERROR)
        {
            throw_socket_error("listen");
        }
        return listener;
    }

    Socket connect_sync(std::string_view host, std::uint16_t port)
    {
        Socket result = create_tcp_socket();
        const sockaddr_in address = make_address(host, port);
        if (connect(result.get(), reinterpret_cast<const sockaddr*>(&address), sizeof(address)) == SOCKET_ERROR)
        {
            throw_socket_error("connect");
        }
        return result;
    }

    Socket accept_sync(const Socket& listener)
    {
        Socket client(accept(listener.get(), nullptr, nullptr));
        if (!client.valid())
        {
            throw_socket_error("accept");
        }
        return client;
    }

    void send_line(const Socket& socket, std::string_view message)
    {
        const std::string payload = std::string(message) + '\n';
        std::size_t sent = 0;
        while (sent < payload.size())
        {
            const int result = send(socket.get(), payload.data() + sent, static_cast<int>(payload.size() - sent), 0);
            if (result == SOCKET_ERROR)
            {
                throw_socket_error("send");
            }
            if (result == 0)
            {
                throw std::runtime_error("Connection closed while sending a message");
            }
            sent += static_cast<std::size_t>(result);
        }
    }

    std::string receive_line(const Socket& socket)
    {
        std::string message;
        std::array<char, 512> buffer{};

        while (message.find('\n') == std::string::npos)
        {
            const int received = recv(socket.get(), buffer.data(), static_cast<int>(buffer.size()), 0);
            if (received == 0)
            {
                throw std::runtime_error("Connection closed before a complete message was received");
            }
            if (received == SOCKET_ERROR)
            {
                throw_socket_error("recv");
            }
            append_message_chunk(message, buffer.data(), static_cast<std::size_t>(received));
        }
        return finish_message(std::move(message));
    }

    void connect_async(const Socket& socket, const NetworkEvent& event, std::string_view host, std::uint16_t port)
    {
        if (WSAEventSelect(socket.get(), event.get(), FD_CONNECT | FD_CLOSE) == SOCKET_ERROR)
        {
            throw_socket_error("WSAEventSelect");
        }

        const sockaddr_in address = make_address(host, port);
        if (connect(socket.get(), reinterpret_cast<const sockaddr*>(&address), sizeof(address)) == 0)
        {
            return;
        }
        if (WSAGetLastError() != WSAEWOULDBLOCK)
        {
            throw_socket_error("connect");
        }
        wait_for_event(socket, event, FD_CONNECT);
    }

    Socket accept_async(const Socket& listener, const NetworkEvent& event)
    {
        if (WSAEventSelect(listener.get(), event.get(), FD_ACCEPT | FD_CLOSE) == SOCKET_ERROR)
        {
            throw_socket_error("WSAEventSelect");
        }

        while (true)
        {
            Socket client(accept(listener.get(), nullptr, nullptr));
            if (client.valid())
            {
                return client;
            }
            if (WSAGetLastError() != WSAEWOULDBLOCK)
            {
                throw_socket_error("accept");
            }
            wait_for_event(listener, event, FD_ACCEPT);
        }
    }

    void send_line_async(const Socket& socket, const NetworkEvent& event, std::string_view message)
    {
        if (WSAEventSelect(socket.get(), event.get(), FD_WRITE | FD_CLOSE) == SOCKET_ERROR)
        {
            throw_socket_error("WSAEventSelect");
        }

        const std::string payload = std::string(message) + '\n';
        std::size_t sent = 0;
        while (sent < payload.size())
        {
            const int result = send(socket.get(), payload.data() + sent, static_cast<int>(payload.size() - sent), 0);
            if (result > 0)
            {
                sent += static_cast<std::size_t>(result);
                continue;
            }
            if (result == 0)
            {
                throw std::runtime_error("Connection closed while sending a message");
            }
            if (WSAGetLastError() != WSAEWOULDBLOCK)
            {
                throw_socket_error("send");
            }
            wait_for_event(socket, event, FD_WRITE);
        }
    }

    std::string receive_line_async(const Socket& socket, const NetworkEvent& event)
    {
        if (WSAEventSelect(socket.get(), event.get(), FD_READ | FD_CLOSE) == SOCKET_ERROR)
        {
            throw_socket_error("WSAEventSelect");
        }

        std::string message;
        std::array<char, 512> buffer{};
        while (message.find('\n') == std::string::npos)
        {
            const int received = recv(socket.get(), buffer.data(), static_cast<int>(buffer.size()), 0);
            if (received > 0)
            {
                append_message_chunk(message, buffer.data(), static_cast<std::size_t>(received));
                continue;
            }
            if (received == 0)
            {
                throw std::runtime_error("Connection closed before a complete message was received");
            }
            if (WSAGetLastError() != WSAEWOULDBLOCK)
            {
                throw_socket_error("recv");
            }
            wait_for_event(socket, event, FD_READ);
        }
        return finish_message(std::move(message));
    }

    std::string remote_endpoint(const Socket& socket)
    {
        sockaddr_in address{};
        int addressLength = sizeof(address);
        if (getpeername(socket.get(), reinterpret_cast<sockaddr*>(&address), &addressLength) == SOCKET_ERROR)
        {
            throw_socket_error("getpeername");
        }

        std::array<char, INET_ADDRSTRLEN> host{};
        if (inet_ntop(AF_INET, &address.sin_addr, host.data(), host.size()) == nullptr)
        {
            throw_socket_error("inet_ntop");
        }
        return std::string(host.data()) + ":" + std::to_string(ntohs(address.sin_port));
    }

}
