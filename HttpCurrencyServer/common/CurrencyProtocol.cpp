#include "CurrencyProtocol.h"

#include "ApplicationSupport.h"

#include <cmath>
#include <iomanip>
#include <sstream>
#include <stdexcept>
#include <vector>

namespace
{
    constexpr std::string_view Ready = "READY";
    constexpr std::string_view ServerBusy = "SERVER_BUSY";
    constexpr std::string_view Auth = "AUTH";
    constexpr std::string_view Authenticated = "AUTHENTICATED";
    constexpr std::string_view InvalidCredentials = "INVALID_CREDENTIALS";
    constexpr std::string_view Cooldown = "COOLDOWN";
    constexpr std::string_view Rate = "RATE";
    constexpr std::string_view UnknownCurrency = "UNKNOWN_CURRENCY";
    constexpr std::string_view InvalidRequest = "INVALID_REQUEST";
    constexpr std::string_view RequestLimit = "REQUEST_LIMIT";
    constexpr std::string_view Bye = "BYE";

    std::vector<std::string> split_words(std::string_view message)
    {
        std::istringstream stream{ std::string(message) };
        std::vector<std::string> words;
        for (std::string word; stream >> word;)
        {
            words.push_back(std::move(word));
        }
        return words;
    }

    std::chrono::seconds parse_seconds(const std::string& value)
    {
        std::size_t parsedCharacters = 0;
        const long long seconds = std::stoll(value, &parsedCharacters);
        if (parsedCharacters != value.size() || seconds < 0)
        {
            throw std::runtime_error("Server sent an invalid retry interval");
        }
        return std::chrono::seconds(seconds);
    }
}

namespace protocol
{
    std::string encode_auth_request(const Credentials& credentials)
    {
        if (credentials.username.empty() || credentials.password.empty() ||
            credentials.username.find_first_of(" \t\r\n") != std::string::npos ||
            credentials.password.find_first_of(" \t\r\n") != std::string::npos)
        {
            throw std::invalid_argument("Username and password must be non-empty single words");
        }
        return std::string(Auth) + " " + credentials.username + " " + credentials.password;
    }

    std::optional<Credentials> decode_auth_request(std::string_view message)
    {
        const std::vector<std::string> words = split_words(message);
        if (words.size() != 3 || words[0] != Auth)
        {
            return std::nullopt;
        }
        return Credentials{ words[1], words[2] };
    }

    std::string encode_rate_request(const CurrencyPair& pair)
    {
        return application::uppercase(pair.source) + " " + application::uppercase(pair.target);
    }

    std::optional<CurrencyPair> decode_rate_request(std::string_view message)
    {
        const std::vector<std::string> words = split_words(message);
        if (words.size() != 2)
        {
            return std::nullopt;
        }
        return CurrencyPair{ application::uppercase(words[0]), application::uppercase(words[1]) };
    }

    std::string encode_ready()
    {
        return std::string(Ready);
    }

    std::string encode_server_busy()
    {
        return std::string(ServerBusy);
    }

    std::string encode_authenticated()
    {
        return std::string(Authenticated);
    }

    std::string encode_invalid_credentials()
    {
        return std::string(InvalidCredentials);
    }

    std::string encode_cooldown(std::chrono::seconds retryAfter)
    {
        return std::string(Cooldown) + " " + std::to_string(retryAfter.count());
    }

    std::string encode_rate(const CurrencyPair& pair, double rate)
    {
        std::ostringstream stream;
        stream << Rate << ' ' << pair.source << ' ' << pair.target << ' ' << std::fixed << std::setprecision(6) << rate;
        return stream.str();
    }

    std::string encode_final_rate(const CurrencyPair& pair, double rate, std::chrono::seconds retryAfter)
    {
        return encode_rate(pair, rate) + " " + std::string(RequestLimit) + " " + std::to_string(retryAfter.count());
    }

    std::string encode_unknown_currency()
    {
        return std::string(UnknownCurrency);
    }

    std::string encode_invalid_request()
    {
        return std::string(InvalidRequest);
    }

    std::string encode_bye()
    {
        return std::string(Bye);
    }

    ServerResponse decode_server_response(std::string_view message)
    {
        const std::vector<std::string> words = split_words(message);
        if (words.size() == 1 && words[0] == Ready)
        {
            return { ResponseType::Ready };
        }
        if (words.size() == 1 && words[0] == ServerBusy)
        {
            return { ResponseType::ServerBusy };
        }
        if (words.size() == 1 && words[0] == Authenticated)
        {
            return { ResponseType::Authenticated };
        }
        if (words.size() == 1 && words[0] == InvalidCredentials)
        {
            return { ResponseType::InvalidCredentials };
        }
        if (words.size() == 2 && words[0] == Cooldown)
        {
            ServerResponse response{ ResponseType::Cooldown };
            response.retryAfter = parse_seconds(words[1]);
            return response;
        }
        if ((words.size() == 4 || words.size() == 6) && words[0] == Rate)
        {
            std::size_t parsedCharacters = 0;
            const double value = std::stod(words[3], &parsedCharacters);
            if (parsedCharacters != words[3].size() || !std::isfinite(value) || value <= 0.0)
            {
                throw std::runtime_error("Server sent an invalid currency rate");
            }
            ServerResponse response{ ResponseType::Rate };
            response.source = words[1];
            response.target = words[2];
            response.rate = value;
            if (words.size() == 6)
            {
                if (words[4] != RequestLimit)
                {
                    throw std::runtime_error("Server sent an invalid rate limit response");
                }
                response.retryAfter = parse_seconds(words[5]);
            }
            return response;
        }
        if (words.size() == 1 && words[0] == UnknownCurrency)
        {
            return { ResponseType::UnknownCurrency };
        }
        if (words.size() == 1 && words[0] == InvalidRequest)
        {
            return { ResponseType::InvalidRequest };
        }
        if (words.size() == 1 && words[0] == Bye)
        {
            return { ResponseType::Bye };
        }
        throw std::runtime_error("Server sent an unsupported response");
    }
}
