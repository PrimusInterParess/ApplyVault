using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Jobnet;

public interface IJobnetJobSaveService
{
    Task<SaveJobnetJobResponse?> SaveAsync(
        string id,
        string requestLanguage,
        Guid userId,
        CancellationToken cancellationToken = default);
}
