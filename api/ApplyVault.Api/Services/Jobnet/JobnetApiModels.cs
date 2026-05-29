using System.Text.Json.Serialization;

namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetSearchResponsePayload
{
    [JsonPropertyName("jobAds")]
    public List<JobnetSearchJobPayload>? JobAds { get; set; }

    [JsonPropertyName("totalJobAdCount")]
    public int? TotalJobAdCount { get; set; }

    [JsonPropertyName("searchString")]
    public string? SearchString { get; set; }
}

internal sealed class JobnetSearchJobPayload
{
    [JsonPropertyName("jobAdId")]
    public string? JobAdId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("hiringOrgName")]
    public string? HiringOrgName { get; set; }

    [JsonPropertyName("workPlaceAddress")]
    public string? WorkPlaceAddress { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("postalCode")]
    public int? PostalCode { get; set; }

    [JsonPropertyName("postalDistrictName")]
    public string? PostalDistrictName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("jobAdUrl")]
    public string? JobAdUrl { get; set; }

    [JsonPropertyName("publicationDate")]
    public string? PublicationDate { get; set; }

    [JsonPropertyName("occupation")]
    public string? Occupation { get; set; }

    [JsonPropertyName("workHourPartTime")]
    public bool? WorkHourPartTime { get; set; }

    [JsonPropertyName("isExternal")]
    public bool? IsExternal { get; set; }
}

internal sealed class JobnetDetailResponsePayload
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("publicationDateTime")]
    public string? PublicationDateTime { get; set; }

    [JsonPropertyName("job")]
    public JobnetDetailJobPayload? Job { get; set; }

    [JsonPropertyName("application")]
    public JobnetDetailApplicationPayload? Application { get; set; }

    [JsonPropertyName("employer")]
    public JobnetDetailEmployerPayload? Employer { get; set; }
}

internal sealed class JobnetDetailJobPayload
{
    [JsonPropertyName("classifications")]
    public List<string>? Classifications { get; set; }

    [JsonPropertyName("hiringOrgName")]
    public string? HiringOrgName { get; set; }

    [JsonPropertyName("workPlaceAddress")]
    public string? WorkPlaceAddress { get; set; }

    [JsonPropertyName("occupation")]
    public string? Occupation { get; set; }

    [JsonPropertyName("workHourPartTime")]
    public bool? WorkHourPartTime { get; set; }
}

internal sealed class JobnetDetailApplicationPayload
{
    [JsonPropertyName("urlText")]
    public string? UrlText { get; set; }

    [JsonPropertyName("emailAddress")]
    public string? EmailAddress { get; set; }

    [JsonPropertyName("deadlineDate")]
    public string? DeadlineDate { get; set; }
}

internal sealed class JobnetDetailEmployerPayload
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public JobnetDetailAddressPayload? Address { get; set; }
}

internal sealed class JobnetDetailAddressPayload
{
    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("municipality")]
    public string? Municipality { get; set; }

    [JsonPropertyName("countryName")]
    public string? CountryName { get; set; }
}
