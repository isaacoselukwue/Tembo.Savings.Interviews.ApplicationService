using Bogus;
using NUnit.Framework;
using Services.Applications.Infrastructure;
using Services.Common.Abstractions.Model;

namespace Services.Applications.Tests.Infrastructure;
[TestFixture]
public class CurrencyConverterTests
{
    private CurrencyConverter _sut;
    private Faker _faker;
    private readonly IReadOnlyList<string> _supportedCurrencies = ["GBP", "USD", "EUR"];

    [SetUp]
    public void Setup()
    {
        _sut = new CurrencyConverter();
        _faker = new Faker();
    }

    [TestCase("USD", 0.80)]
    [TestCase("EUR", 0.85)]
    [TestCase("GBP", 1.0)]
    public async Task ConvertAsync_WithSupportedCurrency_ReturnsCorrectlyConvertedAmount(string fromCurrency, decimal rate)
    {
        decimal fromAmount = _faker.Finance.Amount(0.01m, 10000m);
        Money money = new(fromCurrency, fromAmount);
        decimal expectedAmount = fromAmount * rate;

        Result<Money> result = await _sut.ConvertAsync(money, "GBP");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Currency, Is.EqualTo("GBP"));
            Assert.That(result.Value.Amount, Is.EqualTo(expectedAmount));
        });
    }

    [Test]
    public async Task ConvertAsync_WithUnsupportedFromCurrency_ReturnsFailure()
    {
        string unsupportedCurrency;
        do
        {
            unsupportedCurrency = _faker.Finance.Currency().Code;
        } while (_supportedCurrencies.Contains(unsupportedCurrency));

        Money money = new(unsupportedCurrency, _faker.Finance.Amount());

        Result<Money> result = await _sut.ConvertAsync(money, "GBP");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.System, Is.EqualTo("Converter"));
            Assert.That(result.Error.Code, Is.EqualTo("CurrencyNotSupported"));
            Assert.That(result.Error.Description, Does.Contain(unsupportedCurrency));
        });
    }

    [Test]
    public async Task ConvertAsync_WithUnsupportedToCurrency_ReturnsFailure()
    {
        Money money = new("USD", _faker.Finance.Amount());

        Result<Money> result = await _sut.ConvertAsync(money, "EUR");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.System, Is.EqualTo("Converter"));
            Assert.That(result.Error.Code, Is.EqualTo("NotSupported"));
            Assert.That(result.Error.Description, Does.Contain("EUR"));
        });
    }
}