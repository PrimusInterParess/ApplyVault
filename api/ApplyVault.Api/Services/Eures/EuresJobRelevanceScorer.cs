namespace ApplyVault.Api.Services.Eures;

internal static class EuresJobRelevanceScorer
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
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var pattern in GetKeywordMatchPatterns(keyword))
        {
            if (ContainsWholeTerm(text, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetKeywordMatchPatterns(string keyword)
    {
        switch (keyword.Trim().ToLowerInvariant())
        {
            case ".net":
            case "dotnet":
                return [".net", "dotnet", "dot net", "asp.net", "blazor", "c#", "csharp", "c-sharp"];
            case "c#":
                return ["c#", "c-sharp", "csharp"];
            case "ci/cd":
                return ["ci/cd", "ci cd", "cicd"];
            default:
                return [keyword.Trim()];
        }
    }

    private static bool ContainsWholeTerm(string text, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return false;
        }

        var startIndex = 0;

        while (startIndex <= text.Length - term.Length)
        {
            var matchIndex = text.IndexOf(term, startIndex, StringComparison.OrdinalIgnoreCase);

            if (matchIndex < 0)
            {
                return false;
            }

            var beforeOk = matchIndex == 0 || !IsTermCharacter(text[matchIndex - 1]);
            var afterIndex = matchIndex + term.Length;
            var afterOk = afterIndex >= text.Length || !IsTermCharacter(text[afterIndex]);

            if (beforeOk && afterOk)
            {
                return true;
            }

            startIndex = matchIndex + 1;
        }

        return false;
    }

    private static bool IsTermCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '#' or '/';
    }
}
