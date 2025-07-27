using Moq;
using NUnit.Framework;
using Services.AdministratorOne.Abstractions.Model;
using Services.Applications.Adapters;
using Services.Applications.Configuration;
using Services.Common.Abstractions.Model;

namespace Services.Applications.Tests;
[TestFixture]
public class ProductTwoTests : TestBase
{
    private readonly ProductConfig _productTwoConfig = new()
    {
        MinAge = 18,
        MaxAge = null, // No max age here
        MinPayment = new Money("GBP", 0.99m)
    };

    [SetUp]
    public void ProductTwoSetup()
    {
        MockProductConfigProvider.Setup(p => p.GetConfigFor(ProductCode.ProductTwo)).Returns(_productTwoConfig);
    }

    [Test]
    public async Task Process_WithValidApplication_PublishesCompletedEvent()
    {
        Application application = CreateTestApplication(ProductCode.ProductTwo, 50, 100m); // 50 ys old is valid
        string investorId = Faker.Random.Guid().ToString();
        string accountId = Faker.Random.Guid().ToString();

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockAdminTwoAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>())).ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, accountId)));
        MockAdminTwoAdapter.Setup(a => a.CreateAccount(investorId, It.IsAny<ProductCode>(), It.IsAny<Payment>())).ReturnsAsync(Result.Success(accountId));
        MockAdminTwoAdapter.Setup(a => a.ProcessInitialPayment(accountId, It.IsAny<Payment>())).ReturnsAsync(Result.Success());

        await Sut.Process(application);

        MockKycService.Verify(k => k.GetKycReportAsync(It.IsAny<User>()), Times.Never);
        MockAdminTwoAdapter.Verify(a => a.CreateInvestor(application.Applicant, application.Payment, application.ProductCode), Times.Once);
        MockAdminOneAdapter.Verify(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()), Times.Never);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationCompleted>()), Times.Once);
        MockApplicationRepository.Verify(r => r.MarkedAsProcessedAsync(application.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Process_WhenAgeIsInvalid_PublishesRejectedEvent()
    {
        Application application = CreateTestApplication(ProductCode.ProductTwo, 17, 100m); // Too young
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id && e.Error.Description == ValidationMessages.Value.AgeRequirementNotMet)), Times.Once);
        MockAdminTwoAdapter.Verify(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()), Times.Never);
    }

    [Test]
    public async Task Process_WhenAgeIsValidAtBoundary_PublishesCompletedEvent()
    {
        Application application = CreateTestApplication(ProductCode.ProductTwo, 18, 100m); // Exact age floor
        string investorId = Faker.Random.Guid().ToString();
        string accountId = Faker.Random.Guid().ToString();

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockAdminTwoAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()))
            .ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, null)));
        MockAdminTwoAdapter.Setup(a => a.CreateAccount(investorId, It.IsAny<ProductCode>(), It.IsAny<Payment>()))
            .ReturnsAsync(Result.Success(accountId));
        MockAdminTwoAdapter.Setup(a => a.ProcessInitialPayment(accountId, It.IsAny<Payment>())).Returns(Task.FromResult(Result.Success()));

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationCompleted>()), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationRejected>()), Times.Never);
    }

    [Test]
    public async Task Process_WhenPaymentIsBelowMinimum_PublishesRejectedEvent()
    {
        Application application = CreateTestApplication(ProductCode.ProductTwo, 25, 0.50m);
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockCurrencyConverter.Setup(c => c.ConvertAsync(It.IsAny<Money>(), "GBP")).ReturnsAsync((Money from, string to) => Result.Success(from));

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id && e.Error.Description == ValidationMessages.Value.PaymentBelowMinimum)), Times.Once);
    }

    [Test]
    public async Task Process_WhenUnverifiedUserPassesKyc_IsCompletedSuccessfully()
    {
        Application application = CreateTestApplication(ProductCode.ProductTwo, 30, 100m, isVerified: false);
        KycReport kycReport = new(Faker.Random.Guid(), true);
        string investorId = Faker.Random.Guid().ToString();
        string accountId = Faker.Random.Guid().ToString();

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockKycService.Setup(k => k.GetKycReportAsync(application.Applicant)).ReturnsAsync(Result.Success(kycReport));
        MockAdminTwoAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>())).ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, accountId)));
        MockAdminTwoAdapter.Setup(a => a.CreateAccount(investorId, It.IsAny<ProductCode>(), It.IsAny<Payment>())).ReturnsAsync(Result.Success(accountId));
        MockAdminTwoAdapter.Setup(a => a.ProcessInitialPayment(accountId, It.IsAny<Payment>())).ReturnsAsync(Result.Success());

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
        Application application = CreateTestApplication(ProductCode.ProductTwo, 30, 100m);
        string investorId = Faker.Random.Guid().ToString();
        string accountId = Faker.Random.Guid().ToString();
        Error error = new("Administrator", ErrorCodes.InvestorError, "Administrator service failed.");

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        if (failurePoint == "CreateInvestor")
        {
            MockAdminTwoAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>())).ReturnsAsync(Result.Failure<InvestorCreationResult>(error));
        }
        else
        {
            MockAdminTwoAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>())).ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, null)));
            if (failurePoint == "CreateAccount")
            {
                MockAdminTwoAdapter.Setup(a => a.CreateAccount(investorId, It.IsAny<ProductCode>(), It.IsAny<Payment>())).ReturnsAsync(Result.Failure<string>(error));
            }
            else
            {
                MockAdminTwoAdapter.Setup(a => a.CreateAccount(investorId, It.IsAny<ProductCode>(), It.IsAny<Payment>())).ReturnsAsync(Result.Success(accountId));
                MockAdminTwoAdapter.Setup(a => a.ProcessInitialPayment(accountId, It.IsAny<Payment>())).ReturnsAsync(Result.Failure(error));
            }
        }

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id && e.Error.Code == error.Code)), Times.Once);
        MockApplicationRepository.Verify(r => r.MarkedAsProcessedAsync(application.Id, It.IsAny<CancellationToken>()), Times.Never);
    }
    [Test]
    public async Task Process_WhenUnverifiedUserFailsKyc_PublishesKycFailedAndRejectedEvents()
    {
        Application application = CreateTestApplication(ProductCode.ProductTwo, 30, 100m, isVerified: false);
        KycReport kycReport = new(Faker.Random.Guid(), false);
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockKycService.Setup(k => k.GetKycReportAsync(application.Applicant)).ReturnsAsync(Result.Success(kycReport));

        await Sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.Is<KycFailed>(e => e.UserId == application.Applicant.Id)), Times.Once);
        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id)), Times.Once);
    }
    [Test]
    public async Task Process_WhenApplicationIsProcessedTwice_IsIgnored()
    {
        Application application = CreateTestApplication(ProductCode.ProductTwo, 30, 100m);
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true); // Already processed

        await Sut.Process(application);

        MockProductConfigProvider.Verify(p => p.GetConfigFor(It.IsAny<ProductCode>()), Times.Never);
        MockAdminTwoAdapter.Verify(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()), Times.Never);
        MockBus.Verify(b => b.PublishAsync(It.IsAny<DomainEvent>()), Times.Never);
    }
}