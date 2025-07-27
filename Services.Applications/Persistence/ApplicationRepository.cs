using Services.Applications.State;

namespace Services.Applications.Persistence;
internal class ApplicationRepository : IApplicationRepository
{
    public Task<ApplicationSaga?> GetByIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> HasBeenProcessedAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task MarkedAsProcessedAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SaveAsync(ApplicationSaga saga, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
