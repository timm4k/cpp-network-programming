#include "CurrencyServerService.h"

#include "ApplicationSupport.h"
#include "CurrencyCatalog.h"
#include "CurrencyProtocol.h"

#include <atomic>
#include <cmath>
#include <format>
#include <mutex>
#include <optional>
#include <stdexcept>
#include <thread>
#include <utility>

struct CurrencyServerService::SharedState
{
    SharedState(CurrencyServerConfiguration value, LogHandler handler)
        : configuration(std::move(value)), logHandler(std::move(handler))
    {
    }

    CurrencyServerConfiguration configuration;
    LogHandler logHandler;
    CurrencyCatalog catalog;
    std::atomic_size_t activeClients{};
    std::mutex cooldownMutex;
    std::unordered_map<std::string, std::chrono::steady_clock::time_point> blockedUntil;
    std::mutex logMutex;

    void log(std::string message)
    {
        std::scoped_lock lock(logMutex);
        logHandler(message);
    }

    void log_disconnected(std::string_view endpoint, std::string_view username)
    {
        log(std::format(
            "[{}] Disconnected {} as {}", application::current_timestamp(), endpoint, username));
    }

    std::optional<std::chrono::seconds> remaining_cooldown(const std::string& username)
    {
        std::scoped_lock lock(cooldownMutex);
        const auto blocked = blockedUntil.find(username);
        if (blocked == blockedUntil.end())
        {
            return std::nullopt;
        }

        const auto now = std::chrono::steady_clock::now();
        if (blocked->second <= now)
        {
            blockedUntil.erase(blocked);
            return std::nullopt;
        }

        const auto remaining = blocked->second - now;
        return std::chrono::seconds(static_cast<long long>(
            std::ceil(std::chrono::duration<double>(remaining).count())));
    }

    void block(const std::string& username)
    {
        std::scoped_lock lock(cooldownMutex);
        blockedUntil[username] = std::chrono::steady_clock::now() + configuration.reconnectDelay;
    }
};

namespace
{
    class ActiveClientGuard
    {
    public:
        explicit ActiveClientGuard(std::atomic_size_t& activeClients) : activeClients_(activeClients)
        {
        }

        ~ActiveClientGuard()
        {
            activeClients_.fetch_sub(1);
        }

        ActiveClientGuard(const ActiveClientGuard&) = delete;
        ActiveClientGuard& operator=(const ActiveClientGuard&) = delete;

    private:
        std::atomic_size_t& activeClients_;
    };

    bool credentials_are_valid(
        const CurrencyServerConfiguration& configuration,
        const protocol::Credentials& credentials)
    {
        const auto storedPassword = configuration.credentials.find(credentials.username);
        return storedPassword != configuration.credentials.end() && storedPassword->second == credentials.password;
    }
}

CurrencyServerService::CurrencyServerService(
    CurrencyServerConfiguration configuration,
    LogHandler logHandler)
    : state_(std::make_shared<SharedState>(std::move(configuration), std::move(logHandler)))
{
    if (state_->configuration.port == 0)
    {
        throw std::invalid_argument("Server port is required");
    }
    if (state_->configuration.reconnectDelay <= std::chrono::seconds::zero())
    {
        throw std::invalid_argument("Reconnect delay must be greater than zero");
    }
    if (state_->configuration.maxRequestsPerSession == 0)
    {
        throw std::invalid_argument("Maximum requests must be greater than zero");
    }
    if (state_->configuration.maxConnectedClients == 0)
    {
        throw std::invalid_argument("Maximum clients must be greater than zero");
    }
    if (state_->configuration.credentials.empty())
    {
        throw std::invalid_argument("At least one user account is required");
    }
    if (!state_->logHandler)
    {
        throw std::invalid_argument("Log handler is required");
    }
}

[[noreturn]] void CurrencyServerService::run()
{
    network::WinsockRuntime winsock;
    network::TcpListener listener(state_->configuration.port);
    state_->log(std::format(
        "[{}] Server listening on 127.0.0.1:{} with {} request(s) per session and {} client(s) maximum",
        application::current_timestamp(),
        state_->configuration.port,
        state_->configuration.maxRequestsPerSession,
        state_->configuration.maxConnectedClients));

    while (true)
    {
        network::TcpConnection connection = listener.accept();
        const std::string endpoint = connection.remote_endpoint();
        const std::size_t previousCount = state_->activeClients.fetch_add(1);
        if (previousCount >= state_->configuration.maxConnectedClients)
        {
            state_->activeClients.fetch_sub(1);
            try
            {
                connection.send_line(protocol::encode_server_busy());
            }
            catch (const std::exception& error)
            {
                state_->log(std::format(
                    "[{}] Failed to reject {}: {}",
                    application::current_timestamp(), endpoint, error.what()));
            }
            state_->log(std::format(
                "[{}] Rejected {} because the server is full",
                application::current_timestamp(), endpoint));
            continue;
        }

        try
        {
            std::thread(&CurrencyServerService::serve_client, state_, std::move(connection)).detach();
        }
        catch (const std::exception& error)
        {
            state_->activeClients.fetch_sub(1);
            state_->log(std::format(
                "[{}] Failed to start client session for {}: {}",
                application::current_timestamp(), endpoint, error.what()));
        }
    }
}

void CurrencyServerService::serve_client(std::shared_ptr<SharedState> state, network::TcpConnection connection)
{
    ActiveClientGuard activeClient(state->activeClients);
    const std::string endpoint = connection.remote_endpoint();
    std::string username = "unauthenticated";
    state->log(std::format("[{}] Connected {}", application::current_timestamp(), endpoint));

    try
    {
        connection.send_line(protocol::encode_ready());
        const std::optional<std::string> authMessage = connection.receive_line();
        if (!authMessage)
        {
            state->log_disconnected(endpoint, username);
            return;
        }

        const std::optional<protocol::Credentials> credentials = protocol::decode_auth_request(*authMessage);
        if (!credentials || !credentials_are_valid(state->configuration, *credentials))
        {
            connection.send_line(protocol::encode_invalid_credentials());
            state->log(std::format("[{}] Authentication rejected for {}", application::current_timestamp(), endpoint));
            state->log_disconnected(endpoint, username);
            return;
        }

        username = credentials->username;
        if (const auto cooldown = state->remaining_cooldown(username))
        {
            connection.send_line(protocol::encode_cooldown(*cooldown));
            state->log(std::format(
                "[{}] Cooldown rejected {} as {}",
                application::current_timestamp(), endpoint, username));
            state->log_disconnected(endpoint, username);
            return;
        }

        connection.send_line(protocol::encode_authenticated());
        state->log(std::format(
            "[{}] Authenticated {} as {}",
            application::current_timestamp(), endpoint, username));

        std::size_t requestCount = 0;
        while (const std::optional<std::string> message = connection.receive_line())
        {
            if (application::uppercase(application::trim(*message)) == protocol::ExitCommand)
            {
                connection.send_line(protocol::encode_bye());
                break;
            }

            const std::optional<protocol::CurrencyPair> pair = protocol::decode_rate_request(*message);
            if (!pair)
            {
                connection.send_line(protocol::encode_invalid_request());
                state->log(std::format(
                    "[{}] Invalid request by {} from {}",
                    application::current_timestamp(), username, endpoint));
                continue;
            }

            const std::optional<double> rate = state->catalog.find_rate(*pair);
            if (!rate)
            {
                connection.send_line(protocol::encode_unknown_currency());
                state->log(std::format(
                    "[{}] Unknown currency requested by {} from {}: {} {}",
                    application::current_timestamp(), username, endpoint, pair->source, pair->target));
                continue;
            }

            ++requestCount;
            const bool limitReached = requestCount >= state->configuration.maxRequestsPerSession;
            if (limitReached)
            {
                state->block(username);
                connection.send_line(protocol::encode_final_rate(*pair, *rate, state->configuration.reconnectDelay));
            }
            else
            {
                connection.send_line(protocol::encode_rate(*pair, *rate));
            }
            state->log(std::format(
                "[{}] Rate requested by {} from {}: 1 {} = {:.6f} {}",
                application::current_timestamp(), username, endpoint, pair->source, *rate, pair->target));
            if (limitReached)
            {
                state->log(std::format(
                    "[{}] Request limit reached by {} from {}",
                    application::current_timestamp(), username, endpoint));
                break;
            }
        }
    }
    catch (const std::exception& error)
    {
        state->log(std::format(
            "[{}] Connection error for {} as {}: {}",
            application::current_timestamp(), endpoint, username, error.what()));
    }

    state->log_disconnected(endpoint, username);
}
