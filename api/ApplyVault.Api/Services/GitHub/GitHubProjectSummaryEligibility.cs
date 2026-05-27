using System.Text.RegularExpressions;

namespace ApplyVault.Api.Services;

internal static partial class GitHubProjectSummaryEligibility
{
    public const string InsufficientDataMessage =
        "This repository does not include enough information to generate a CV summary. Add a README or a substantive repository description on GitHub, or include topics that explain the project.";

    private const int MinReadmeChars = 60;
    private const int MinStandaloneDescriptionChars = 40;
    private const int MinCombinedDescriptionChars = 20;

    private static readonly HashSet<string> PlaceholderExactMatches = new(StringComparer.OrdinalIgnoreCase)
    {
        "test",
        "testing",
        "todo",
        "wip",
        "sample",
        "demo",
        "playground",
        "temp",
        "tmp",
        "hello world",
        "new repo",
        "my repo",
        "common"
    };

    private static readonly string[] PlaceholderPrefixes =
    [
        "test",
        "testing",
        "todo",
        "sample",
        "demo",
        "wip",
        "temp",
        "tmp",
        "playground"
    ];

    public static bool HasSufficientSummaryData(
        string? readmeText,
        string? description,
        string? primaryLanguage,
        IReadOnlyList<string> topics)
    {
        var trimmedReadme = Normalize(readmeText);
        var trimmedDescription = Normalize(description);
        var hasLanguage = !string.IsNullOrWhiteSpace(primaryLanguage);
        var meaningfulTopics = GetMeaningfulTopics(topics);

        if (IsSubstantiveReadme(trimmedReadme))
        {
            return true;
        }

        if (IsSubstantiveStandaloneDescription(trimmedDescription))
        {
            return true;
        }

        if (IsSubstantiveCombinedDescription(trimmedDescription, hasLanguage, meaningfulTopics.Count > 0))
        {
            return true;
        }

        if (meaningfulTopics.Count >= 2 && (hasLanguage || IsSubstantiveCombinedDescription(trimmedDescription, false, true)))
        {
            return true;
        }

        return false;
    }

    public static bool IsWeakGeneratedSummary(CvProjectSummaryResult result, GitHubProjectAiInput input)
    {
        if (!result.SufficientContext)
        {
            return false;
        }

        var bullets = result.Bullets?.Where((bullet) => !string.IsNullOrWhiteSpace(bullet)).ToArray() ?? [];
        var summary = Normalize(result.Summary);
        var description = Normalize(input.Description);

        if (bullets.Length == 0 && (summary?.Length ?? 0) < 80)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(description) &&
            !string.IsNullOrWhiteSpace(summary) &&
            summary.Contains(description, StringComparison.OrdinalIgnoreCase) &&
            bullets.Length == 0)
        {
            return true;
        }

        return false;
    }

    private static bool IsSubstantiveReadme(string? readmeText) =>
        !string.IsNullOrWhiteSpace(readmeText) &&
        readmeText.Length >= MinReadmeChars &&
        !IsPlaceholderText(readmeText);

    private static bool IsSubstantiveStandaloneDescription(string? description) =>
        !string.IsNullOrWhiteSpace(description) &&
        description.Length >= MinStandaloneDescriptionChars &&
        !IsPlaceholderText(description);

    private static bool IsSubstantiveCombinedDescription(
        string? description,
        bool hasLanguage,
        bool hasTopics)
    {
        if (string.IsNullOrWhiteSpace(description) ||
            description.Length < MinCombinedDescriptionChars ||
            IsPlaceholderText(description))
        {
            return false;
        }

        return hasLanguage || hasTopics;
    }

    private static List<string> GetMeaningfulTopics(IReadOnlyList<string> topics) =>
        topics
            .Select(Normalize)
            .Where((topic) => !string.IsNullOrWhiteSpace(topic) && !IsPlaceholderText(topic))
            .Select((topic) => topic!)
            .ToList();

    private static bool IsPlaceholderText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var normalized = text.Trim();
        var lower = normalized.ToLowerInvariant();

        if (PlaceholderExactMatches.Contains(lower))
        {
            return true;
        }

        if (normalized.Length <= 15)
        {
            return true;
        }

        foreach (var prefix in PlaceholderPrefixes)
        {
            if (lower.StartsWith(prefix, StringComparison.Ordinal) && normalized.Length < 32)
            {
                return true;
            }
        }

        if (PlaceholderWordRegex().IsMatch(lower))
        {
            return normalized.Length < 36;
        }

        return false;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"\b(test|testing|todo|demo|sample|wip|temp|tmp|playground)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderWordRegex();
}
