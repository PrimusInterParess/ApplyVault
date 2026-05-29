using System.Net;
using System.Text.Json;
using ApplyVault.Api.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetApiClient(
    HttpClient httpClient,
    IOptions<JobnetIntegrationOptions> options)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<JobnetSearchResponsePayload?> SearchAsync(
        string searchString,
        int pageNumber,
        int resultsPerPage,
        CancellationToken cancellationToken = default)
    {
        var integrationOptions = options.Value;
        var query = BuildSearchQuery(
            searchString,
            pageNumber,
            resultsPerPage,
            integrationOptions);
        var requestUri = BuildUri($"{integrationOptions.SearchPath}?{query}");

        using var response = await SendGetWithRetryAsync(requestUri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new JobnetJobClientException(
                $"Jobnet search failed with status {(int)response.StatusCode} for {TruncateUri(requestUri)}. {TruncateResponseBody(responseBody)}");
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<JobnetSearchResponsePayload>(
            contentStream,
            SerializerOptions,
            cancellationToken);
    }

    public async Task<JobnetDetailResponsePayload?> GetJobByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var integrationOptions = options.Value;
        var encodedId = Uri.EscapeDataString(id.Trim());
        var detailPath = integrationOptions.DetailPathTemplate.Replace("{id}", encodedId, StringComparison.Ordinal);

        var requestUri = BuildUri(detailPath);
        using var response = await SendGetWithRetryAsync(requestUri, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new JobnetJobClientException(
                $"Jobnet detail failed with status {(int)response.StatusCode} for {TruncateUri(requestUri)}. {TruncateResponseBody(responseBody)}");
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<JobnetDetailResponsePayload>(
            contentStream,
            SerializerOptions,
            cancellationToken);
    }

    public async Task<JobnetSearchJobPayload?> FindSearchJobByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalizedId = id.Trim();
        var searchResponse = await SearchAsync(normalizedId, pageNumber: 1, resultsPerPage: 10, cancellationToken);

        return searchResponse?.JobAds?
            .FirstOrDefault((job) => string.Equals(job.JobAdId, normalizedId, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildSearchQuery(
        string searchString,
        int pageNumber,
        int resultsPerPage,
        JobnetIntegrationOptions integrationOptions)
    {
        var queryParams = new Dictionary<string, string?>
        {
            ["searchString"] = searchString.Trim(),
            ["resultsPerPage"] = Math.Max(1, resultsPerPage).ToString(),
            ["pageNumber"] = Math.Max(1, pageNumber).ToString(),
            ["orderType"] = ResolveOrderType(integrationOptions.DefaultOrderType),
            ["kmRadius"] = Math.Max(1, integrationOptions.DefaultKmRadius).ToString()
        };

        return QueryHelpers.AddQueryString(string.Empty, queryParams).TrimStart('?');
    }

    private Uri BuildUri(string relativePath)
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        var normalizedPath = relativePath.StartsWith('/') ? relativePath : $"/{relativePath}";
        return new Uri($"{baseUrl}{normalizedPath}");
    }

    private static string ResolveOrderType(string? configuredOrderType)
    {
        if (string.IsNullOrWhiteSpace(configuredOrderType))
        {
            return "BestMatch";
        }

        var orderType = configuredOrderType.Trim();
        return string.Equals(orderType, "CreationDate", StringComparison.OrdinalIgnoreCase)
            ? "PublicationDate"
            : orderType;
    }

    private async Task<HttpResponseMessage> SendGetWithRetryAsync(
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Clamp(options.Value.SearchMaxRetryAttempts, 1, 5);
        HttpResponseMessage? response = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            response = await httpClient.GetAsync(requestUri, cancellationToken);

            if (response.IsSuccessStatusCode
                || !IsTransientUpstreamFailure(response.StatusCode)
                || attempt >= maxAttempts)
            {
                return response;
            }

            await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();

            var delayMs = Math.Min(2000, 250 * (1 << (attempt - 1)));
            await Task.Delay(delayMs, cancellationToken);
        }

        return response!;
    }

    private static bool IsTransientUpstreamFailure(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static string TruncateUri(Uri uri)
    {
        var value = uri.ToString();
        return value.Length <= 160 ? value : $"{value[..160]}...";
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
