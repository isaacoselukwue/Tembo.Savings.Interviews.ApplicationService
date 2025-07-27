# Log

## Asummptions
- **AdministratorOne amounts:** the provider expects integers we assume they expect pence so we convert decimal value by 100. We also treat the first entry in the `User.BankAccounts` as primary account for funding source.
- **AccountValidation:** we assume the administrators can perform balance/lien status on accounts, can debit any account currency and debit will succeed. We also assume currency is in ISO format eg EUR, GBP, USD
- **NINO Validation:** we assume that providers can validate Nino (eg with HMRC api) and out of scope for processor.
- **Age calculations:** we calculate based on Europe/London time zone

## Decisions
- **Factory pattern:** we setup a factory to enable swapping of product to admin without editing `ApplicationProcessor`.
- **Config driven for requirements:** centralised age config, min payments, admin mapping from DI.
- **Domain Events for all outcomes:** downstream will have a complete audit trail and power analytics/notification services. Eg opting to publish `UserVerified` so that users status can be updated.
- **`IApplicationRepository` idempotency check:** we assume that the repository will handle duplicate submissions and not process the same application multiple times.
- **Extras:** fcy support, result pattern inplace of throwing exceptions.
- **DI registration:** added support for DI registration so can be registered as a library easily
- **Tests:** tests data are generated using bogus, parameterised to reduce duplication and are handle age/payment boundaries. Also added CI file to trigger `dotnet test` for project.

## Observations
- The `Services.AdministratorOne.Abstractions` has only one synchronous method, standardise error codes, unlike the `Services.AdministratorTwo.Abstractions`.
- Provider `Services.AdministratorTwo.Abstractions` does not support deleting account if debit fails.

## Todo
- Complete the application sagas for robust handling so that partial failures can be retried.
- Add structured logging, health checks and traces support for events lifecycle, external calls.
- Add circuit breaker (polly) for external services and graceful handling (eg to dead letter queue)
- Create proper mock for bus ensuring that events are raised in order eg from investor created to application completed

## To run
``` Bash
dotnet restore
dotnet test
```