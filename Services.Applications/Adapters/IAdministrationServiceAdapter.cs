global using Services.Common.Abstractions.Model;

namespace Services.Applications.Adapters;
public interface IAdministrationServiceAdapter
{
    Task<Result<InvestorCreationResult>> CreateInvestor(User user, Payment payment, ProductCode productCode);
    Task<Result<string>> CreateAccount(string investorId, ProductCode product, Payment initialPayment);
    Task<Result> ProcessInitialPayment(string accountId, Payment payment);
}