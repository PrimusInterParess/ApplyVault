using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Eures;

public interface IEuresJobSearchRequestNormalizer
{
    bool TryNormalizeSearchRequest(
        EuresJobSearchRequest request,
        out EuresJobSearchRequest normalizedRequest,
        out string validationMessage);

    string NormalizeRequestLanguage(string? requestLanguage);
}
