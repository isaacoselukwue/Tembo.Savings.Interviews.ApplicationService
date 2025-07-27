using Services.AdministratorOne.Abstractions;
using Services.AdministratorOne.Abstractions.Model;

namespace Services.Applications.Adapters;
internal class AdministratorOneAdapter(IAdministrationService adminOneService) : IAdministrationServiceAdapter
{
    private readonly IAdministrationService _adminOneService = adminOneService;

    public Task<Result<InvestorCreationResult>> CreateInvestor(User user, Payment payment, ProductCode productCode)
    {
        CreateInvestorRequest request = new()
        {
            Reference = user.Id.ToString(),
            FirstName = user.Forename,
            LastName = user.Surname,
            DateOfBirth = user.DateOfBirth.ToString("yyyy-MM-dd"),
            Nino = user.Nino,
            Addressline1 = user.Addresses.FirstOrDefault()?.Addressline1 ?? "N/A",
            Addressline2 = user.Addresses.FirstOrDefault()?.Addressline2 ?? "N/A",
            Addressline3 = user.Addresses.FirstOrDefault()?.Addressline3 ?? "N/A",
            Addressline4 = user.Addresses.FirstOrDefault()?.Country ?? "N/A",
            PostCode = user.Addresses.FirstOrDefault()?.PostCode ?? "N/A",
            Email = "N/A",
            MobileNumber = "N/A",
            Product = productCode.ToString(),
            SortCode = payment.BankAccount.SortCode,
            AccountNumber = payment.BankAccount.AccountNumber,
            InitialPayment = (int)(payment.Amount.Amount * 100)
        };

        CreateInvestorResponse response = _adminOneService.CreateInvestor(request);
        if(!string.Equals(response.Reference, request.Reference, StringComparison.OrdinalIgnoreCase))
        {
            Error error = new("AdministratorOne", ErrorCodes.InvestorError, "CreateInvestor called out of order or failed previously.");
            return Task.FromResult(Result.Failure<InvestorCreationResult>(error));
        }

        return Task.FromResult(Result.Success(new InvestorCreationResult(response.InvestorId, response.AccountId)));
    }

    public Task<Result<string>> CreateAccount(string investorId, ProductCode productCode, Payment payment)
    {
        return Task.FromResult(Result.Failure<string>(new Error("AdministratorOne", ErrorCodes.AccountError, "CreateAccount called out of order or failed previously.")));
    }

    public Task<Result> ProcessInitialPayment(string accountId, Payment payment)
    {
        return Task.FromResult(Result.Success());
    }
}