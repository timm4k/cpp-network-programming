#pragma once

#include <chrono>
#include <cstdint>
#include <optional>
#include <string>
#include <string_view>

namespace protocol
{
    inline constexpr std::uint16_t DefaultPort = 8080;
    inline constexpr std::chrono::seconds ReconnectDelay{ 60 };
    inline constexpr std::string_view ExitCommand = "EXIT";
    inline constexpr std::string_view SupportedCurrencies = "USD, EURO, UAH, GBP, PLN";

    struct Credentials
    {
        std::string username;
        std::string password;
    };

    struct CurrencyPair
    {
        std::string source;
        std::string target;
    };

    enum class ResponseType
    {
        Ready,
        ServerBusy,
        Authenticated,
        InvalidCredentials,
        Cooldown,
        Rate,
        UnknownCurrency,
        InvalidRequest,
        Bye
    };

    struct ServerResponse
    {
        ResponseType type = ResponseType::InvalidRequest;
        std::string source;
        std::string target;
        double rate = 0.0;
        std::chrono::seconds retryAfter{};
    };

    std::string encode_auth_request(const Credentials& credentials);
    std::optional<Credentials> decode_auth_request(std::string_view message);
    std::string encode_rate_request(const CurrencyPair& pair);
    std::optional<CurrencyPair> decode_rate_request(std::string_view message);

    std::string encode_ready();
    std::string encode_server_busy();
    std::string encode_authenticated();
    std::string encode_invalid_credentials();
    std::string encode_cooldown(std::chrono::seconds retryAfter);
    std::string encode_rate(const CurrencyPair& pair, double rate);
    std::string encode_final_rate(const CurrencyPair& pair, double rate, std::chrono::seconds retryAfter);
    std::string encode_unknown_currency();
    std::string encode_invalid_request();
    std::string encode_bye();
    ServerResponse decode_server_response(std::string_view message);
}
