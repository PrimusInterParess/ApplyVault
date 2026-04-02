using ApplyVault.Api.Data;

namespace ApplyVault.Api.Services;

public interface IAppUserService
{
    Task<AppUserEntity> GetRequiredUserAsync(CancellationToken cancellationToken = default);

    Task<AppUserEntity?> TryGetCurrentUserAsync(CancellationToken cancellationToken = default);
}
