#include "SocketTransport.h"

#include <array>
#include <limits>
#include <stdexcept>
#include <thread>
#include <utility>

namespace
{
    [[noreturn]] void throw_socket_error(std::string_view operation, int code = WSAGetLastError())
    {
        throw std::runtime_error(std::string(operation) + " failed with Winsock error " + std::to_string(code));
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

    network::Socket create_socket()
    {
        network::Socket result(socket(AF_INET, SOCK_STREAM, IPPROTO_TCP));
        if (!result.valid())
        {
            throw_socket_error("socket");
        }
        return result;
    }

    std::optional<std::string> extract_line(std::string& pending)
    {
        const std::size_t newline = pending.find('\n');
        if (newline == std::string::npos)
        {
            return std::nullopt;
        }
        if (newline > network::MaxLineSize)
        {
            throw std::runtime_error("Received message exceeds the allowed size");
        }

        std::string line = pending.substr(0, newline);
        pending.erase(0, newline + 1);
        if (!line.empty() && line.back() == '\r')
        {
            line.pop_back();
        }
        return line;
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

    TcpConnection::TcpConnection(Socket socket) : socket_(std::move(socket))
    {
    }

    void TcpConnection::send_line(std::string_view message) const
    {
        if (message.size() > MaxLineSize)
        {
            throw std::invalid_argument("Message exceeds the allowed size");
        }

        const std::string payload = std::string(message) + '\n';
        std::size_t sent = 0;
        while (sent < payload.size())
        {
            const int result = send(socket_.get(), payload.data() + sent, static_cast<int>(payload.size() - sent), 0);
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

    std::optional<std::string> TcpConnection::receive_line()
    {
        if (std::optional<std::string> line = extract_line(pending_))
        {
            return line;
        }

        std::array<char, 512> buffer{};
        while (true)
        {
            const int received = recv(socket_.get(), buffer.data(), static_cast<int>(buffer.size()), 0);
            if (received == 0)
            {
                if (pending_.empty())
                {
                    return std::nullopt;
                }
                throw std::runtime_error("Connection closed before a complete message was received");
            }
            if (received == SOCKET_ERROR)
            {
                throw_socket_error("recv");
            }

            pending_.append(buffer.data(), static_cast<std::size_t>(received));
            if (std::optional<std::string> line = extract_line(pending_))
            {
                return line;
            }
            if (pending_.size() > MaxLineSize)
            {
                throw std::runtime_error("Received message exceeds the allowed size");
            }
        }
    }

    std::string TcpConnection::remote_endpoint() const
    {
        sockaddr_in address{};
        int addressLength = sizeof(address);
        if (getpeername(socket_.get(), reinterpret_cast<sockaddr*>(&address), &addressLength) == SOCKET_ERROR)
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

    TcpListener::TcpListener(std::uint16_t port) : socket_(create_socket())
    {
        const sockaddr_in address = make_address("127.0.0.1", port);
        if (bind(socket_.get(), reinterpret_cast<const sockaddr*>(&address), sizeof(address)) == SOCKET_ERROR)
        {
            throw_socket_error("bind");
        }
        if (listen(socket_.get(), SOMAXCONN) == SOCKET_ERROR)
        {
            throw_socket_error("listen");
        }
    }

    TcpConnection TcpListener::accept() const
    {
        Socket client(WSAAccept(socket_.get(), nullptr, nullptr, nullptr, 0));
        if (!client.valid())
        {
            throw_socket_error("accept");
        }
        return TcpConnection(std::move(client));
    }

    std::uint16_t parse_server_port(int argc, char* argv[], std::uint16_t defaultPort)
    {
        if (argc > 2)
        {
            throw std::invalid_argument("Usage: CurrencyServer [port]");
        }
        return argc == 2 ? parse_port(argv[1]) : defaultPort;
    }

    ClientEndpoint parse_client_endpoint(int argc, char* argv[], std::uint16_t defaultPort)
    {
        if (argc > 3)
        {
            throw std::invalid_argument("Usage: CurrencyClient [host] [port]");
        }

        ClientEndpoint endpoint{ "127.0.0.1", defaultPort };
        if (argc >= 2)
        {
            endpoint.host = argv[1];
        }
        if (argc == 3)
        {
            endpoint.port = parse_port(argv[2]);
        }
        return endpoint;
    }

    TcpConnection connect_with_retry(const ClientEndpoint& endpoint, std::chrono::milliseconds retryDelay)
    {
        const sockaddr_in address = make_address(endpoint.host, endpoint.port);
        while (true)
        {
            Socket socket = create_socket();
            if (::connect(socket.get(), reinterpret_cast<const sockaddr*>(&address), sizeof(address)) == 0)
            {
                return TcpConnection(std::move(socket));
            }

            const int error = WSAGetLastError();
            if (error != WSAECONNREFUSED)
            {
                throw_socket_error("connect", error);
            }
            std::this_thread::sleep_for(retryDelay);
        }
    }
}
