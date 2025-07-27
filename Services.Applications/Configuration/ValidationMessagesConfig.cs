namespace Services.Applications.Configuration;
public class ValidationMessagesConfig
{
    public string AgeRequirementNotMet { get; init; } = "Applicant does not meet age requirements.";
    public string PaymentBelowMinimum { get; init; } = "Payment amount is below the minimum required.";
    public string KycServiceFailed { get; init; } = "A failure occurred while attempting to verify the user with the KYC service.";
    public string KycVerificationFailed { get; init; } = "The application was rejected because the user failed KYC verification.";
    public string NoAdministratorConfigured { get; init; } = "No administrator service configured for this product.";
}