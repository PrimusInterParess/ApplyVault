using ApplyVault.Api.Models.InterviewPrep;

namespace ApplyVault.Api.Services.InterviewPrep;

/// <summary>Persisted structured study-brief body (camelCase JSON). Nested topics only.</summary>
internal sealed record InterviewPrepStudyBriefBodyStorage(
    IReadOnlyList<InterviewPrepStudyBriefTopicDto> Topics);
