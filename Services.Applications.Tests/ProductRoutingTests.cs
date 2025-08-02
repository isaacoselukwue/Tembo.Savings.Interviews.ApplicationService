using Moq;
using NUnit.Framework;
using Services.Applications.Adapters;
using Services.Applications.Configuration;
using Services.Common.Abstractions.Model;

namespace Services.Applications.Tests;
[TestFixture]
public class ProductRoutingTests : TestBase
{
    [Test]
    public async Task Process_WhenProductOneIsRoutedToAdminTwo_CallsCorrectAdapter()
    {
        Dictionary<ProductCode, IAdministrationServiceAdapter> customAdminAdapters = new()
        {
            [ProductCode.ProductOne] = MockAdminTwoAdapter.Object,
            [ProductCode.ProductTwo] = MockAdminOneAdapter.Object
        };

        ApplicationProcessor sut = new(MockKycService.Object, MockBus.Object, MockProductConfigProvider.Object, customAdminAdapters,
            MockCurrencyConverter.Object, MockApplicationRepository.Object, ValidationMessages);

        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 100m);
        string investorId = Faker.Random.Guid().ToString();
        string accountId = Faker.Random.Guid().ToString();

        MockProductConfigProvider.Setup(p => p.GetConfigFor(ProductCode.ProductOne))
            .Returns(new ProductConfig() { MinAge = 18, MaxAge = 39, MinPayment = new Money("GBP", 0.99m) });

        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockAdminTwoAdapter.Setup(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>())).ReturnsAsync(Result.Success(new InvestorCreationResult(investorId, null)));
        MockAdminTwoAdapter.Setup(a => a.CreateAccount(investorId, It.IsAny<ProductCode>(), It.IsAny<Payment>())).ReturnsAsync(Result.Success(accountId));
        MockAdminTwoAdapter.Setup(a => a.ProcessInitialPayment(accountId, It.IsAny<Payment>())).ReturnsAsync(Result.Success());

        await sut.Process(application);

        MockAdminTwoAdapter.Verify(a => a.CreateInvestor(application.Applicant, application.Payment, application.ProductCode), Times.Once);

        MockAdminOneAdapter.Verify(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()), Times.Never);

        MockBus.Verify(b => b.PublishAsync(It.IsAny<ApplicationCompleted>()), Times.Once);
    }

    [Test]
    public async Task Process_WhenNoAdministratorIsConfigured_PublishesRejectedEvent()
    {
        ApplicationProcessor sut = new(MockKycService.Object, MockBus.Object, MockProductConfigProvider.Object,
            new Dictionary<ProductCode, IAdministrationServiceAdapter>(), // no config
            MockCurrencyConverter.Object, MockApplicationRepository.Object, ValidationMessages);

        Application application = CreateTestApplication(ProductCode.ProductOne, 25, 100m);
        MockApplicationRepository.Setup(r => r.HasBeenProcessedAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        MockProductConfigProvider.Setup(p => p.GetConfigFor(ProductCode.ProductOne)).Returns(new ProductConfig { MinAge = 18, MaxAge = 39, MinPayment = new Money("GBP", 0.99m) });

        await sut.Process(application);

        MockBus.Verify(b => b.PublishAsync(It.Is<ApplicationRejected>(e => e.ApplicationId == application.Id && e.Error.Description == ValidationMessages.Value.NoAdministratorConfigured)), Times.Once);
        MockAdminOneAdapter.Verify(a => a.CreateInvestor(It.IsAny<User>(), It.IsAny<Payment>(), It.IsAny<ProductCode>()), Times.Never);
    }
}