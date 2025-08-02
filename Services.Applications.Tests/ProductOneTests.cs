using Moq;
using NUnit.Framework;
using Services.AdministratorOne.Abstractions.Model;
using Services.Applications.Adapters;
using Services.Applications.Configuration;
using Services.Common.Abstractions.Model;

namespace Services.Applications.Tests;

[TestFixture]
public class ProductOneTests : TestBase
{
    private readonly ProductConfig _productOneConfig = new()
    {
        MinAge = 18,
        MaxAge = 39,
        MinPayment = new Money("GBP", 0.99m)
    };

    [SetUp]
    public void ProductOneSetup()
    {
        MockProductConfigProvider.Setup(p => p.GetConfigFor(ProductCode.ProductOne)).Returns(_productOneConfig);
    }

    [Test]
    public async Task Process_WithValidApplication_PublishesCompletedEvent()
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 100m);
        string investorId = Faker.Random.Guid().ToString();
        string accountId = Faker.Random.Guid().ToString();

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockAdminOneAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>())).ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, accountId)));
        MockAdminOneAdapter.Setup(a => a.CreateAccount(investorId, It.IsAny<ProductCode>(), It.IsAny<Payment>())).ReturnsAsync(Result.Success(accountId));
        MockAdminOneAdapter.Setup(a => a.ProcessInitialPayment(accountId, It.IsAny<Payment>())).ReturnsAsync(Result.Success());

        await Sut.Process(application);

        MockKycService.Verify(k => k.GetKycReportAsync(It.IsAny<User>()), Times.Never);
        MockAdminOneAdapter.Verify(a => a.CreateInvestor(application.Applicant, application.Payment, application.ProductCode), Times.Once);
        MockAdminOneAdapter.Verify(a => a.CreateAccount(It.IsAny<string>(), It.IsAny<ProductCode>(), It.IsAny<Payment>()), Times.Never);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationCompleted>()), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationRejected>()), Times.Never);
        MockApplicationRepository.Verify(r => r.MarkedAsProcessedAsync(application.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(17)] // too young
    [TestCase(40)] // too old
    public async Task Process_WhenAgeIsInvalid_PublishesRejectedEvent(int age)
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, age, 100m);
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id && e.Error.Description == ValidationMessages.Value.AgeRequirementNotMet)), Times.Once);
        MockAdminOneAdapter.Verify(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()), Times.Never);
    }

    [TestCase(18)] // Exact age floor/max
    [TestCase(39)]
    public async Task Process_WhenAgeIsValidAtBoundaries_PublishesCompletedEvent(int age)
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, age, 100m);
        string investorId = Faker.Random.Guid().ToString();
        string accountId = Faker.Random.Guid().ToString();

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockAdminOneAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()))
            .ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, accountId)));
        MockAdminOneAdapter.Setup(a => a.ProcessInitialPayment(accountId, It.IsAny<Payment>())).Returns(Task.FromResult(Result.Success()));

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationCompleted>()), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationRejected>()), Times.Never);
    }

    [Test]
    public async Task Process_WhenPaymentIsBelowMinimum_PublishesRejectedEvent()
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 0.50m); // Below 0.99
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockCurrencyConverter.Setup(c => c.ConvertAsync(It.IsAny<Money>(), "GBP")).ReturnsAsync((Money from, string to) => Result.Success(from));

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id && e.Error.Description == ValidationMessages.Value.PaymentBelowMinimum)), Times.Once);
    }

    [Test]
    public async Task Process_WhenPaymentInForeignCurrencySufficient_PublishesCompletedEvent()
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 1.50m, currency: "USD");
        string investorId = Faker.Random.Guid().ToString();
        string accountId = Faker.Random.Guid().ToString();

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockCurrencyConverter.Setup(c => c.ConvertAsync(application.Payment.Amount, "GBP")).ReturnsAsync(Result.Success(new Money("GBP", 1.10m)));

        MockAdminOneAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()))
            .ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, accountId)));
        MockAdminOneAdapter.Setup(a => a.ProcessInitialPayment(accountId, It.IsAny<Payment>())).Returns(Task.FromResult(Result.Success()));

        await Sut.Process(application);

        MockCurrencyConverter.Verify(c => c.ConvertAsync(application.Payment.Amount, "GBP"), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationCompleted>()), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationRejected>()), Times.Never);
    }

    [Test]
    public async Task Process_WhenCurrencyConversionFails_PublishesRejectedEvent()
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 1.50m, currency: "USD");
        Error error = new("Converter", "Failure", "External service unavailable.");

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockCurrencyConverter.Setup(c => c.ConvertAsync(application.Payment.Amount, "GBP")).ReturnsAsync(Result.Failure<Money>(error));

        await Sut.Process(application);

        MockCurrencyConverter.Verify(c => c.ConvertAsync(application.Payment.Amount, "GBP"), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id && e.Error.Description == ValidationMessages.Value.PaymentBelowMinimum)), Times.Once);
    }

    [Test]
    public async Task Process_WhenPaymentInForeignCurrencyInsufficient_PublishesRejectedEvent()
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 1.00m, currency: "USD");
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockCurrencyConverter.Setup(c => c.ConvertAsync(application.Payment.Amount, "GBP")).ReturnsAsync(Result.Success(new Money("GBP", 0.80m)));

        await Sut.Process(application);

        MockCurrencyConverter.Verify(c => c.ConvertAsync(application.Payment.Amount, "GBP"), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id && e.Error.Description == ValidationMessages.Value.PaymentBelowMinimum)), Times.Once);
    }

    [Test]
    public async Task Process_WhenUnverifiedUserFailsKyc_PublishesKycFailedAndRejectedEvents()
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 100m, isVerified: false);
        KycReport kycReport = new(Faker.Random.Guid(), false);
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockKycService.Setup(k => k.GetKycReportAsync(application.Applicant)).ReturnsAsync(Result.Success(kycReport));

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.Is<KycFailed>(e => e.UserId == application.Applicant.Id)), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id)), Times.Once);
    }

    [Test]
    public async Task Process_WhenKycServiceFails_PublishesRejectedEvent()
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 100m, isVerified: false);
        Error error = new("Kyc", "Service", "Service unavailable.");
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockKycService.Setup(k => k.GetKycReportAsync(application.Applicant)).ReturnsAsync(Result.Failure<KycReport>(error));

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id && e.Error.Description == ValidationMessages.Value.KycServiceFailed)), Times.Once);
        MockAdminOneAdapter.Verify(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()), Times.Never);
    }

    [Test]
    public async Task Process_WhenUnverifiedUserPassesKyc_IsCompletedSuccessfully()
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 100m, isVerified: false);
        KycReport kycReport = new(Faker.Random.Guid(), true); // KYC passes
        string investorId = Faker.Random.Guid().ToString();
        string accountId = Faker.Random.Guid().ToString();

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockKycService.Setup(k => k.GetKycReportAsync(application.Applicant)).ReturnsAsync(Result.Success(kycReport));
        MockAdminOneAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>())).ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, accountId)));
        MockAdminOneAdapter.Setup(a => a.CreateAccount(investorId, It.IsAny<ProductCode>(), It.IsAny<Payment>())).ReturnsAsync(Result.Success(accountId));
        MockAdminOneAdapter.Setup(a => a.ProcessInitialPayment(accountId, It.IsAny<Payment>())).ReturnsAsync(Result.Success());

        await Sut.Process(application);

        MockKycService.Verify(k => k.GetKycReportAsync(application.Applicant), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.Is<UserVerified>(e => e.UserId == application.Applicant.Id)), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationCompleted>()), Times.Once);
    }

    [TestCase("CreateInvestor")]
    [TestCase("CreateAccount")]
    [TestCase("ProcessInitialPayment")]
    public async Task Process_WhenAdministratorFails_PublishesRejectedEvent(string failurePoint)
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 100m);
        string investorId = Faker.Random.Guid().ToString();
        string accountId = Faker.Random.Guid().ToString();
        Error error = new("AdministratorOne", ErrorCodes.InvestorError, "Administrator service failed.");

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        if (failurePoint == "CreateInvestor")
        {
            MockAdminOneAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>())).ReturnsAsync(Result.Failure<InvestorCreationResult>(error));
        }
        else if (failurePoint == "CreateAccount")
        {
            MockAdminOneAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>())).ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, null)));
            MockAdminOneAdapter.Setup(a => a.CreateAccount(investorId, It.IsAny<ProductCode>(), It.IsAny<Payment>())).ReturnsAsync(Result.Failure<string>(error));
        }
        else
        {
            MockAdminOneAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>())).ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, accountId)));
            MockAdminOneAdapter.Setup(a => a.ProcessInitialPayment(accountId, It.IsAny<Payment>())).Returns(Task.FromResult(Result.Failure(error)));
        }

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id && e.Error.Code == error.Code)), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationCompleted>()), Times.Never);
        MockApplicationRepository.Verify(r => r.MarkedAsProcessedAsync(application.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Process_WhenApplicationIsProcessedTwice_IsIgnored()
    {
        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 100m);
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true); // Already processed

        await Sut.Process(application);

        MockProductConfigProvider.Verify(p => p.GetConfigFor(It.IsAny<ProductCode>()), Times.Never);
        MockAdminOneAdapter.Verify(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()), Times.Never);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<DomainEvent>()), Times.Never);
    }
}