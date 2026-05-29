using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Jobnet;

public interface IJobnetJobSearchRequestNormalizer
{
    bool TryNormalizeSearchRequest(
        JobnetJobSearchRequest request,
        out JobnetJobSearchRequest normalizedRequest,
        out string validationMessage);

    string NormalizeRequestLanguage(string? requestLanguage);
}
