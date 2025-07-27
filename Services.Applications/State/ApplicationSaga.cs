namespace Services.Applications.State;
public enum SagaStatus
{
    Pending,
    AgeValidated,
    PaymentValidated,
    KycVerified,
    InvestorCreated,
    AccountCreated,
    Completed,
    Rejected
}

public class ApplicationSaga(Application application)
{
    public Guid Id { get; init; } = application.Id;
    public Application ApplicationData { get; init; } = application;
    public SagaStatus Status { get; set; } = SagaStatus.Pending;
    public string? InvestorId { get; set; }
    public string? AccountId { get; set; }
    public string? RejectionReason { get; set; }
}