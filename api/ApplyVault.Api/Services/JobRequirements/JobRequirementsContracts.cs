using System.Text.Json.Serialization;

namespace ApplyVault.Api.Services.JobRequirements;

// Explicit numeric values are stable database identifiers.
internal enum JobRequirementsStatus
{
    Uninitialized = 0,
    DescriptionNeeded = 1,
    Pending = 2,
    Processing = 3,
    Ready = 4,
    Failed = 5
}

// JSON names are explicitly mapped in JobRequirementsJson.
internal enum JobRequirementCategory
{
    [JsonStringEnumMemberName("responsibilitiesOutcomes")] ResponsibilitiesOutcomes = 0,
    [JsonStringEnumMemberName("skillsToolsKnowledge")] SkillsToolsKnowledge = 1,
    [JsonStringEnumMemberName("experienceSeniority")] ExperienceSeniority = 2,
    [JsonStringEnumMemberName("educationCertificationsLanguages")] EducationCertificationsLanguages = 3,
    [JsonStringEnumMemberName("teamProductIndustry")] TeamProductIndustry = 4,
    [JsonStringEnumMemberName("interviewProcess")] InterviewProcess = 5
}

internal enum RequirementImportance
{
    [JsonStringEnumMemberName("unspecified")] Unspecified = 0,
    [JsonStringEnumMemberName("required")] Required = 1,
    [JsonStringEnumMemberName("preferred")] Preferred = 2
}

internal sealed record JobRequirementFact(
    JobRequirementCategory Category,
    string Value,
    RequirementImportance Importance,
    string Evidence);

internal sealed record JobRequirementsDocument(int SchemaVersion, IReadOnlyList<JobRequirementFact> Facts);

internal sealed record JobRequirementsResult(JobRequirementsStatus Status, JobRequirementsDocument? Document);

internal interface IJobRequirementsReader
{
    Task<JobRequirementsResult?> GetCurrentAsync(Guid jobId, Guid userId, CancellationToken cancellationToken = default);
}

internal interface IJobRequirementsAiClient
{
    Task<string> ExtractAsync(string description, CancellationToken cancellationToken);
}
