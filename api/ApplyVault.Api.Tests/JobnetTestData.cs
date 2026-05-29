using System.Text.Json;
using ApplyVault.Api.Services.Jobnet;

namespace ApplyVault.Api.Tests;

internal static class JobnetTestData
{
    public static JobnetSearchJobPayload CreateSearchJob(
        string id,
        string title,
        string? employer = null,
        string? description = null,
        string? publicationDate = "2026-01-15T00:00:00+01:00",
        string? jobAdUrl = null)
    {
        return new JobnetSearchJobPayload
        {
            JobAdId = id,
            Title = title,
            HiringOrgName = employer,
            Description = description,
            PublicationDate = publicationDate,
            JobAdUrl = jobAdUrl,
            Country = "Danmark"
        };
    }

    public static JobnetDetailResponsePayload CreateDetailJob(
        string title,
        string? employer = null,
        string? body = null,
        bool workInDenmark = true,
        string? applicationUrl = null,
        string? email = null)
    {
        return new JobnetDetailResponsePayload
        {
            Title = title,
            Body = body,
            PublicationDateTime = "2026-01-15T00:00:00+01:00",
            Job = new JobnetDetailJobPayload
            {
                Classifications = workInDenmark
                    ? ["WorkInDenmark", "EURES"]
                    : ["EURES"],
                HiringOrgName = employer,
                WorkHourPartTime = false,
                Occupation = "Software developer"
            },
            Employer = new JobnetDetailEmployerPayload
            {
                Name = employer,
                Address = new JobnetDetailAddressPayload
                {
                    City = "Copenhagen",
                    CountryName = "Danmark"
                }
            },
            Application = new JobnetDetailApplicationPayload
            {
                UrlText = applicationUrl,
                EmailAddress = email
            }
        };
    }

    public static string SerializeSearchResponse(JobnetSearchResponsePayload payload) =>
        JsonSerializer.Serialize(payload);

    public static string SerializeDetailResponse(JobnetDetailResponsePayload payload) =>
        JsonSerializer.Serialize(payload);
}
