# Log

## Asummptions
- **AdminstratorOne amounts:** the provider expects integers we assume they expect pence so we convert decimal value by 100. We also treat the first entry in the `User.BankAccounts` as primary account for funding source.
- **AccountValidation:** we assume the adminstrators can perform balance/lien status on accounts, can debit any account currency and debit will succeed.
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
- The `Services.AdminstratorOne.Abstractions` has only one synchronous method, standardise error codes, unlike the `Services.AdminstratorTwo.Abstractions`.
- Provider `Services.AdminstratorTwo.Abstractions` does not support deleting account if debit fails.

## Todo
- Complete the application sagas for roburst handling so that partial failures can be retried.
- Add structured logging, health checks and traces support for events lifecycle, external calls.
- Add circuit breaker (polly) for external services and graceful handling (eg to dead letter queue)
- Create proper mock for bus ensuring that events are raised in order eg from investor created to application completed

## To run
``` Bash
dotnet restore
dotnet test
```