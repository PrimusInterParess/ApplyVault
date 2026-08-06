using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Models.InterviewPrep;

public sealed record InterviewPrepGenerateStudyBriefRequest(
    InterviewPrepLanguage Language,
    InterviewPrepMarket Market,
    Guid? ScrapeResultId = null,
    string? FocusNote = null);

public sealed record InterviewPrepRegenerateStudyBriefRequest(
    string? FocusNote = null,
    InterviewPrepLanguage? Language = null,
    InterviewPrepMarket? Market = null);

/// <summary>Shared leaf for Coverage items, sample questions, and talking points.</summary>
public sealed record InterviewPrepStudyBriefItemDto(
    string Text,
    string? Note = null);

public sealed record InterviewPrepStudyBriefTopicDto(
    string Name,
    string Gap,
    int Priority,
    string? Note,
    IReadOnlyList<InterviewPrepStudyBriefItemDto> CoverageItems,
    IReadOnlyList<InterviewPrepStudyBriefItemDto> SampleQuestions,
    IReadOnlyList<InterviewPrepStudyBriefItemDto> TalkingPoints);

public sealed record InterviewPrepStudyBriefDto(
    Guid Id,
    Guid? ScrapeResultId,
    string? JobTitle,
    string? CompanyName,
    string Language,
    string Market,
    string? FocusNoteSnapshot,
    bool Outdated,
    IReadOnlyList<string> OutdatedReasons,
    DateTimeOffset GeneratedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<InterviewPrepStudyBriefTopicDto> Topics,
    bool UsedAiFallback);

public sealed record InterviewPrepStudyBriefListResponseDto(
    IReadOnlyList<InterviewPrepStudyBriefDto> Items);
