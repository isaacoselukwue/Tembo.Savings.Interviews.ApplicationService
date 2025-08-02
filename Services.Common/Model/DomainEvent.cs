namespace Services.Common.Abstractions.Model;

public abstract record DomainEvent;

public record InvestorCreated(Guid UserId, string InvestorId) : DomainEvent;

public record AccountCreated(string InvestorId, ProductCode Product, string AccountId) : DomainEvent;

public record KycFailed(Guid UserId, Guid ReportId) : DomainEvent;

public record UserVerified(Guid UserId, Guid KycReportId) : DomainEvent;

public record ApplicationCompleted(Guid ApplicationId) : DomainEvent;

public record ApplicationRejected : DomainEvent
{
    public Guid ApplicationId { get; }
    public Error Error { get; }

    public ApplicationRejected(Guid applicationId, Error error)
    {
        ApplicationId = applicationId;
        Error = error;
    }

    public ApplicationRejected(Guid applicationId, string reason)
    {
        ApplicationId = applicationId;
        Error = new Error("Application.Validation", "ValidationFailed", reason);
    }
}