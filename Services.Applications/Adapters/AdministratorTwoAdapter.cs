using Services.AdministratorTwo.Abstractions;

namespace Services.Applications.Adapters;
internal class AdministratorTwoAdapter : IAdministrationServiceAdapter
{
    private readonly IAdministrationService _adminTwoService;

    public AdministratorTwoAdapter(IAdministrationService adminTwoService)
    {
        _adminTwoService = adminTwoService;
    }

    public async Task<Result<InvestorCreationResult>> CreateInvestor(User user, Payment payment, ProductCode productCode)
    {
        Result<Guid> investorResult = await _adminTwoService.CreateInvestorAsync(user);
        if (!investorResult.IsSuccess)
        {
            return Result.Failure<InvestorCreationResult>(investorResult.Error);
        }
        return Result.Success(new InvestorCreationResult(investorResult.Value.ToString(), string.Empty));
    }

    public async Task<Result<string>> CreateAccount(string investorId, ProductCode productCode, Payment initialPayment)
    {
        if (!Guid.TryParse(investorId, out Guid investorGuid))
        {
            Error error = new("AdministratorTwo", ApplicationErrorCodes.AccountCreationFailure, "Invalid Investor ID format for AdministratorTwo.");
            return Result.Failure<string>(error);
        }
        Result<Guid> accountResult = await _adminTwoService.CreateAccountAsync(investorGuid, productCode);
        if (!accountResult.IsSuccess)
        {
            return Result.Failure<string>(accountResult.Error);
        }
        return Result.Success(accountResult.Value.ToString());
    }

    public async Task<Result> ProcessInitialPayment(string accountId, Payment payment)
    {
        if (!Guid.TryParse(accountId, out Guid accountGuid))
        {
            Error error = new("AdministratorTwo", ApplicationErrorCodes.PaymentProcessingFailure, "Invalid Account ID format for AdministratorTwo.");
            return Result.Failure(error);
        }
        Result<Guid> paymentResult = await _adminTwoService.ProcessPaymentAsync(accountGuid, payment);
        if (!paymentResult.IsSuccess)
        {
            return Result.Failure(paymentResult.Error);
        }
        return Result.Success();
    }
}
