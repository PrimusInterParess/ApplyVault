using System.Net;
using System.Text;
using System.Text.Json;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Eures;

internal sealed class EuresApiClient(
    HttpClient httpClient,
    IOptions<EuresIntegrationOptions> options)
{
    private static readonly JsonSerializerOptions RequestSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ResponseSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<EuresSearchResponsePayload?> SearchAsync(
        EuresSearchPayload payload,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            BuildUri("/jv-searchengine/public/jv-search/search"),
            new StringContent(
                JsonSerializer.Serialize(payload, RequestSerializerOptions),
                Encoding.UTF8,
                "application/json"),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new EuresJobClientException(
                $"EURES search failed with status {(int)response.StatusCode}. {TruncateResponseBody(responseBody)}");
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<EuresSearchResponsePayload>(
            contentStream,
            ResponseSerializerOptions,
            cancellationToken);
    }

    public async Task<EuresDetailResponsePayload?> GetJobByIdAsync(
        string id,
        string requestLanguage,
        CancellationToken cancellationToken = default)
    {
        var encodedId = Uri.EscapeDataString(id.Trim());
        var language = string.IsNullOrWhiteSpace(requestLanguage) ? "en" : requestLanguage.Trim();

        using var response = await httpClient.GetAsync(
            BuildUri($"/jv-searchengine/public/jv/id/{encodedId}?requestLang={Uri.EscapeDataString(language)}"),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new EuresJobClientException(
                $"EURES detail failed with status {(int)response.StatusCode}.");
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<EuresDetailResponsePayload>(
            contentStream,
            ResponseSerializerOptions,
            cancellationToken);
    }

    public static EuresSearchPayload BuildSearchPayload(
        string keyword,
        int resultsPerPage,
        int page,
        string sortSearch,
        string locationCode,
        string requestLanguage)
    {
        return new EuresSearchPayload
        {
            ResultsPerPage = resultsPerPage,
            Page = page,
            SortSearch = sortSearch,
            Keywords =
            [
                new EuresKeywordPayload
                {
                    Keyword = EuresKeywordSearchExpander.NormalizeForSearch(keyword),
                    SpecificSearchCode = "EVERYWHERE"
                }
            ],
            LocationCodes = [NormalizeLocationCode(locationCode)],
            PublicationPeriod = null,
            MinNumberPost = null,
            SessionId = $"applyvault-{Guid.NewGuid():N}",
            RequestLanguage = string.IsNullOrWhiteSpace(requestLanguage) ? "en" : requestLanguage.Trim()
        };
    }

    private Uri BuildUri(string relativePath)
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        return new Uri($"{baseUrl}{relativePath}");
    }

    private static string NormalizeLocationCode(string locationCode)
    {
        return locationCode.Trim().ToLowerInvariant();
    }

    private static string TruncateResponseBody(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        var trimmedBody = responseBody.Trim();
        return trimmedBody.Length <= 240 ? trimmedBody : $"{trimmedBody[..240]}...";
    }
}
