using ApplyVault.Api.Data;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvPdfProjectsMergeService
{
    Task<CvDocumentDto> MergeProjectsAsync(AppUserEntity user, CancellationToken cancellationToken = default);
}
