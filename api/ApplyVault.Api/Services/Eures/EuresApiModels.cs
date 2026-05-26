namespace ApplyVault.Api.Services.Eures;

internal sealed class EuresSearchPayload
{
    public int ResultsPerPage { get; set; }

    public int Page { get; set; }

    public string SortSearch { get; set; } = "MOST_RECENT";

    public IReadOnlyList<EuresKeywordPayload> Keywords { get; set; } = [];

    public object? PublicationPeriod { get; set; }

    public IReadOnlyList<string> OccupationUris { get; set; } = [];

    public IReadOnlyList<string> SkillUris { get; set; } = [];

    public IReadOnlyList<string> RequiredExperienceCodes { get; set; } = [];

    public IReadOnlyList<string> PositionScheduleCodes { get; set; } = [];

    public IReadOnlyList<string> SectorCodes { get; set; } = [];

    public IReadOnlyList<string> EducationAndQualificationLevelCodes { get; set; } = [];

    public IReadOnlyList<string> PositionOfferingCodes { get; set; } = [];

    public IReadOnlyList<string> LocationCodes { get; set; } = [];

    public IReadOnlyList<string> EuresFlagCodes { get; set; } = [];

    public IReadOnlyList<string> OtherBenefitsCodes { get; set; } = [];

    public IReadOnlyList<string> RequiredLanguages { get; set; } = [];

    public object? MinNumberPost { get; set; }

    public string SessionId { get; set; } = string.Empty;

    public string RequestLanguage { get; set; } = "en";
}

internal sealed class EuresKeywordPayload
{
    public string Keyword { get; set; } = string.Empty;

    public string SpecificSearchCode { get; set; } = "EVERYWHERE";
}

internal sealed class EuresSearchResponsePayload
{
    public int NumberRecords { get; set; }

    public IReadOnlyList<EuresSearchJobPayload>? Jvs { get; set; }
}

internal sealed class EuresSearchJobPayload
{
    public string? Id { get; set; }

    public string? Title { get; set; }

    public long? CreationDate { get; set; }

    public Dictionary<string, string[]>? LocationMap { get; set; }

    public EuresEmployerPayload? Employer { get; set; }

    public string? PreferredLanguage { get; set; }

    public Dictionary<string, EuresJobProfilePayload>? JvProfiles { get; set; }

    public Dictionary<string, EuresTranslationPayload>? Translations { get; set; }
}

internal sealed class EuresDetailResponsePayload
{
    public string? Id { get; set; }

    public long? CreationDate { get; set; }

    public string? PreferredLanguage { get; set; }

    public Dictionary<string, EuresJobProfilePayload>? JvProfiles { get; set; }
}

internal sealed class EuresJobProfilePayload
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? PositionOfferingCode { get; set; }

    public IReadOnlyList<string>? PositionScheduleCodes { get; set; }

    public IReadOnlyList<EuresLocationPayload>? Locations { get; set; }

    public EuresEmployerPayload? Employer { get; set; }

    public IReadOnlyList<string>? ApplicationInstructions { get; set; }
}

internal sealed class EuresTranslationPayload
{
    public string? Title { get; set; }

    public string? Description { get; set; }
}

internal sealed class EuresEmployerPayload
{
    public string? Name { get; set; }
}

internal sealed class EuresLocationPayload
{
    public string? CountryCode { get; set; }

    public string? Region { get; set; }

    public string? CityName { get; set; }

    public string? PostalCode { get; set; }
}
