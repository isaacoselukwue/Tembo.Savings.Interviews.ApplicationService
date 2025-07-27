using Microsoft.Extensions.Options;
using Services.Applications.Adapters;
using Services.Applications.Configuration;
using Services.Applications.Persistence;
using Services.Common.Abstractions.Abstractions;

namespace Services.Applications;

public class ApplicationProcessor(IKycService kycService, IBus bus, IProductConfigProvider productConfigProvider,
    IReadOnlyDictionary<ProductCode, IAdministrationServiceAdapter> adminServiceAdapters, ICurrencyConverter currencyConverter,
    IApplicationRepository applicationRepository, IOptions<ValidationMessagesConfig> validationMessages) : IApplicationProcessor
{
    private readonly IKycService _kycService = kycService;
    private readonly IBus _bus = bus;
    private readonly ICurrencyConverter _currencyConverter = currencyConverter;
    private readonly IProductConfigProvider _productConfigProvider = productConfigProvider;
    private readonly IReadOnlyDictionary<ProductCode, IAdministrationServiceAdapter> _adminServiceAdapters = adminServiceAdapters;
    private readonly IApplicationRepository _applicationRepository = applicationRepository;
    private readonly ValidationMessagesConfig _validationMessages = validationMessages.Value;

    public async Task Process(Application application)
    {
        if (await _applicationRepository.HasBeenProcessedAsync(application.Id))
        {
            return;
        }

        var productConfig = _productConfigProvider.GetConfigFor(application.ProductCode);

        if (!IsAgeValid(application.Applicant, productConfig))
        {
            await _bus.PublishAsync(new ApplicationRejected(application.Id, _validationMessages.AgeRequirementNotMet));
            return;
        }

        if (!await IsPaymentValidAsync(application.Payment, productConfig.MinPayment))
        {
            await _bus.PublishAsync(new ApplicationRejected(application.Id, _validationMessages.PaymentBelowMinimum));
            return;
        }

        if (!await EnsureUserIsVerifiedAsync(application))
        {
            return;
        }

        if (!_adminServiceAdapters.TryGetValue(application.ProductCode, out IAdministrationServiceAdapter? adminServiceAdapter))
        {
            await _bus.PublishAsync(new ApplicationRejected(application.Id, _validationMessages.NoAdministratorConfigured));
            return;
        }

        await ProcessWithAdministratorAsync(application, adminServiceAdapter);
    }
    private static bool IsAgeValid(User applicant, ProductConfig config)
    {
        TimeZoneInfo londonZone;
        try
        {
            londonZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        }
        catch (TimeZoneNotFoundException)
        {
            londonZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        }

        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, londonZone));
        int age = today.Year - applicant.DateOfBirth.Year;

        if (applicant.DateOfBirth > today.AddYears(-age))
            age--;
        if (age < config.MinAge) 
            return false;
        if (config.MaxAge.HasValue && age > config.MaxAge.Value) 
            return false;

        return true;
    }

    private async Task<bool> EnsureUserIsVerifiedAsync(Application application)
    {
        if (application.Applicant.IsVerified.GetValueOrDefault())
            return true;

        Result<KycReport> kycResult = await _kycService.GetKycReportAsync(application.Applicant);
        if (!kycResult.IsSuccess)
        {
            await _bus.PublishAsync(new ApplicationRejected(application.Id, _validationMessages.KycServiceFailed));
            return false;
        }
        if (!kycResult.Value.IsVerified)
        {
            await _bus.PublishAsync(new KycFailed(application.Applicant.Id, kycResult.Value.Id));
            await _bus.PublishAsync(new ApplicationRejected(application.Id, _validationMessages.KycVerificationFailed));
            return false;
        }
        application.Applicant.IsVerified = true;
        await _bus.PublishAsync(new UserVerified(application.Applicant.Id, kycResult.Value.Id));
        return true;
    }

    private async Task ProcessWithAdministratorAsync(Application application, IAdministrationServiceAdapter adminServiceAdapter)
    {
        Result<InvestorCreationResult> investorResult = await adminServiceAdapter.CreateInvestor(application.Applicant, application.Payment, application.ProductCode);
        if (!investorResult.IsSuccess)
        {
            await _bus.PublishAsync(new ApplicationRejected(application.Id, investorResult.Error));
            return;
        }
        await _bus.PublishAsync(new InvestorCreated(application.Applicant.Id, investorResult.Value.InvestorId));

        string accountId = investorResult.Value.AccountId ?? string.Empty;
        if (string.IsNullOrEmpty(accountId))
        {
            Result<string> accountResult = await adminServiceAdapter.CreateAccount(investorResult.Value.InvestorId, application.ProductCode, application.Payment);
            if (!accountResult.IsSuccess)
            {
                await _bus.PublishAsync(new ApplicationRejected(application.Id, accountResult.Error));
                return;
            }
            accountId = accountResult.Value;
        }
        await _bus.PublishAsync(new AccountCreated(investorResult.Value.InvestorId, application.ProductCode, accountId));

        Result paymentResult = await adminServiceAdapter.ProcessInitialPayment(accountId, application.Payment);
        if (!paymentResult.IsSuccess)
        {
            await _bus.PublishAsync(new ApplicationRejected(application.Id, paymentResult.Error));
            return;
        }

        await _bus.PublishAsync(new ApplicationCompleted(application.Id));
        await _applicationRepository.MarkedAsProcessedAsync(application.Id);
    }

    private async Task<bool> IsPaymentValidAsync(Payment applicationPayment, Money minimumPayment)
    {
        Money paymentInRequiredCurrency = applicationPayment.Amount;

        if (!string.Equals(applicationPayment.Amount.Currency, minimumPayment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            Result<Money> conversionResult = await _currencyConverter.ConvertAsync(applicationPayment.Amount, minimumPayment.Currency);
            if (!conversionResult.IsSuccess)
            {
                return false;
            }
            paymentInRequiredCurrency = conversionResult.Value;
        }

        return paymentInRequiredCurrency.Amount >= minimumPayment.Amount;
    }
}