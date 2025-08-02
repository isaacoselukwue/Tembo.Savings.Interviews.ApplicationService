using Services.Applications.State;

namespace Services.Applications.Persistence;
public interface IApplicationRepository
{
    Task<ApplicationSaga?> GetByIdAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<bool> HasBeenProcessedAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task MarkedAsProcessedAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task SaveAsync(ApplicationSaga saga, CancellationToken cancellationToken = default);
}