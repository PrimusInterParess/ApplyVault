namespace ApplyVault.Api.Services.Jobnet;

internal static class JobnetJobRelevanceScorer
{
    public static int CalculateScore(
        string? title,
        string? employer,
        string? description,
        IReadOnlyList<string> keywords)
    {
        var score = 0;

        foreach (var keyword in keywords)
        {
            if (ContainsKeyword(title, keyword))
            {
                score += 3;
                continue;
            }

            if (ContainsKeyword(description, keyword))
            {
                score += 2;
                continue;
            }

            if (ContainsKeyword(employer, keyword))
            {
                score += 1;
            }
        }

        return score;
    }

    private static bool ContainsKeyword(string? text, string keyword)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        return text.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
