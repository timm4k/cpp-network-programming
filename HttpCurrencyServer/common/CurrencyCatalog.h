#pragma once

#include "CurrencyProtocol.h"

#include <optional>
#include <string>
#include <unordered_map>

class CurrencyCatalog
{
public:
    CurrencyCatalog();
    [[nodiscard]] std::optional<double> find_rate(const protocol::CurrencyPair& pair) const;

private:
    std::unordered_map<std::string, double> unitsPerUsd_;
};
