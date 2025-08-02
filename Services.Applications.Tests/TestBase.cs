using Bogus;
using Bogus.Extensions.UnitedKingdom;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Services.Applications.Adapters;
using Services.Applications.Configuration;
using Services.Applications.Persistence;
using Services.Common.Abstractions.Abstractions;
using Services.Common.Abstractions.Model;

namespace Services.Applications.Tests;
public abstract class TestBase
{
    protected Mock<IKycService> MockKycService;
    protected Mock<IBus> MockBus;
    protected Mock<IProductConfigProvider> MockProductConfigProvider;
    protected Mock<IAdministrationServiceAdapter> MockAdminOneAdapter;
    protected Mock<IAdministrationServiceAdapter> MockAdminTwoAdapter;
    protected Mock<ICurrencyConverter> MockCurrencyConverter;
    protected Mock<IApplicationRepository> MockApplicationRepository;
    protected IOptions<ValidationMessagesConfig> ValidationMessages;
    protected ApplicationProcessor Sut;
    protected Faker Faker;

    [SetUp]
    public void Setup()
    {
        Randomizer.Seed = new Random(42);
        Faker.GlobalUniqueIndex = 0;
        Faker = new Faker();
        MockKycService = new Mock<IKycService>();
        MockBus = new Mock<IBus>();
        MockProductConfigProvider = new Mock<IProductConfigProvider>();
        MockAdminOneAdapter = new Mock<IAdministrationServiceAdapter>();
        MockAdminTwoAdapter = new Mock<IAdministrationServiceAdapter>();
        MockCurrencyConverter = new Mock<ICurrencyConverter>();
        MockApplicationRepository = new Mock<IApplicationRepository>();

        ValidationMessages = Options.Create(new ValidationMessagesConfig
        {
            AgeRequirementNotMet = "Age requirement not met",
            PaymentBelowMinimum = "Payment below minimum",
            KycServiceFailed = "KYC service failed",
            KycVerificationFailed = "KYC verification failed"
        });

        Dictionary<ProductCode, IAdministrationServiceAdapter> adminAdapters = new()
        {
            [ProductCode.ProductOne] = MockAdminOneAdapter.Object,
            [ProductCode.ProductTwo] = MockAdminTwoAdapter.Object
        };

        Sut = new ApplicationProcessor(
            MockKycService.Object,
            MockBus.Object,
            MockProductConfigProvider.Object,
            adminAdapters,
            MockCurrencyConverter.Object,
            MockApplicationRepository.Object,
            ValidationMessages);
    }

    protected Application CreateTestApplication(ProductCode productCode, int age, decimal paymentAmount, string currency = "GBP", bool isVerified = true)
    {
        BankAccount bankAccount = new() { SortCode = Faker.Finance.SortCode(), AccountNumber = Faker.Finance.Account() };
        Payment payment = new(bankAccount, new Money(currency, paymentAmount));
        Address address = new()
        {
            Addressline1 = $"{Faker.Address.BuildingNumber()} {Faker.Address.StreetName()}",
            Addressline2 = Faker.Address.SecondaryAddress(),
            Addressline3 = Faker.Address.City(),
            PostCode = Faker.Address.ZipCode("### ###"),
            Country = Faker.Address.CountryOfUnitedKingdom()
        };

        return new Application
        {
            Id = Faker.Random.Guid(),
            ProductCode = productCode,
            Applicant = new()
            {
                Id = Faker.Random.Guid(),
                DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-age)),
                IsVerified = isVerified,
                Addresses = [address],
                BankAccounts = [bankAccount],
                Forename = Faker.Name.FirstName(),
                Surname = Faker.Name.LastName(),
                Nino = GenerateNino(Faker)
            },
            Payment = payment
        };
    }

    private static string GenerateNino(Faker faker)
    {
        const string validLetters = "ABCEGHJKLMNPRSTWXYZ";
        const string suffixLetters = "ABCD";
        string[] invalidPrefixes = { "BG", "GB", "KN", "NK", "NT", "TN", "ZZ" };

        string prefix;
        do
        {
            var first = validLetters[faker.Random.Int(0, validLetters.Length - 1)];
            var second = validLetters[faker.Random.Int(0, validLetters.Length - 1)];
            prefix = $"{first}{second}";
        }
        while (Array.Exists(invalidPrefixes, x => x == prefix));

        var numberPart = faker.Random.Int(0, 999999).ToString("D6");
        var suffix = suffixLetters[faker.Random.Int(0, suffixLetters.Length - 1)];

        return $"{prefix}{numberPart}{suffix}";
    }
}