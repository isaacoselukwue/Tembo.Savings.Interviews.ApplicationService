using Services.Common.Abstractions.Model;

namespace Services.Common.Abstractions.Abstractions;
public interface ICurrencyConverter
{
    Task<Result<Money>> ConvertAsync(Money from, string toCurrency);
}