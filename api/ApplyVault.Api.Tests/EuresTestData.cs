using ApplyVault.Api.Services.Eures;

namespace ApplyVault.Api.Tests;

internal static class EuresTestData
{
    public const long SampleCreationDate = 1_705_276_800_000;

    public static EuresSearchJobPayload CreateSearchJob(
        string id,
        string title,
        string? employer = null,
        string? description = null,
        string language = "en",
        long? creationDate = SampleCreationDate)
    {
        return new EuresSearchJobPayload
        {
            Id = id,
            Title = title,
            CreationDate = creationDate,
            PreferredLanguage = language,
            Employer = employer is null ? null : new EuresEmployerPayload { Name = employer },
            JvProfiles = new Dictionary<string, EuresJobProfilePayload>
            {
                [language] = new()
                {
                    Title = title,
                    Description = description,
                    Employer = employer is null ? null : new EuresEmployerPayload { Name = employer },
                    Locations =
                    [
                        new EuresLocationPayload
                        {
                            CityName = "Copenhagen",
                            CountryCode = "DK"
                        }
                    ]
                }
            }
        };
    }

    public static EuresDetailResponsePayload CreateDetailJob(
        string id,
        string title,
        string? employer = null,
        string? description = null,
        string language = "en")
    {
        return new EuresDetailResponsePayload
        {
            Id = id,
            CreationDate = SampleCreationDate,
            PreferredLanguage = language,
            JvProfiles = new Dictionary<string, EuresJobProfilePayload>
            {
                [language] = new()
                {
                    Title = title,
                    Description = description,
                    Employer = employer is null ? null : new EuresEmployerPayload { Name = employer },
                    PositionOfferingCode = "PERMANENT",
                    PositionScheduleCodes = ["FULLTIME"],
                    ApplicationInstructions = ["Apply at <a href=\"https://jobs.example.com/apply\">company site</a>"]
                }
            }
        };
    }

    public static string SerializeSearchResponse(EuresSearchResponsePayload payload) =>
        System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

    public static string SerializeDetailResponse(EuresDetailResponsePayload payload) =>
        System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
}
