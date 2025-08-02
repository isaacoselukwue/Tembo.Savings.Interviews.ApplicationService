using Services.Common.Abstractions.Abstractions;

namespace Services.Applications.Infrastructure;
public class CurrencyConverter : ICurrencyConverter
{
    private static readonly IReadOnlyDictionary<string, decimal> GbpExchangeRates = new Dictionary<string, decimal>
    {
        { "GBP", 1.0m },
        { "USD", 0.80m },
        { "EUR", 0.85m }
    };

    public Task<Result<Money>> ConvertAsync(Money from, string toCurrency)
    {
        if (toCurrency != "GBP")
        {
            Error error = new("Converter", "NotSupported", $"Conversion to '{toCurrency}' is not supported.");
            return Task.FromResult(Result.Failure<Money>(error));
        }
        if (!GbpExchangeRates.TryGetValue(from.Currency, out var rate))
        {
            Error error = new("Converter", "CurrencyNotSupported", $"Conversion from currency '{from.Currency}' is not supported.");
            return Task.FromResult(Result.Failure<Money>(error));
        }
        decimal convertedValue = from.Amount * rate;
        Money convertedAmount = new(toCurrency, convertedValue);

        return Task.FromResult(Result.Success(convertedAmount));
    }
}
