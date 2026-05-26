using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Eures;

public interface IEuresJobSaveService
{
    Task<SaveEuresJobResponse?> SaveAsync(
        string id,
        string requestLanguage,
        Guid userId,
        CancellationToken cancellationToken = default);
}
