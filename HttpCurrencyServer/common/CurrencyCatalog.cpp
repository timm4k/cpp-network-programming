#include "CurrencyCatalog.h"

CurrencyCatalog::CurrencyCatalog() : unitsPerUsd_
{
    { "USD", 1.0 },
    { "EURO", 0.92 },
    { "UAH", 41.50 },
    { "GBP", 0.78 },
    { "PLN", 3.95 }
}
{
}

std::optional<double> CurrencyCatalog::find_rate(const protocol::CurrencyPair& pair) const
{
    const auto source = unitsPerUsd_.find(pair.source);
    const auto target = unitsPerUsd_.find(pair.target);
    if (source == unitsPerUsd_.end() || target == unitsPerUsd_.end())
    {
        return std::nullopt;
    }
    return target->second / source->second;
}
