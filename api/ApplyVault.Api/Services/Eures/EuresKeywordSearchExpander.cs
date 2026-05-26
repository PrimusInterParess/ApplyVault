namespace ApplyVault.Api.Services.Eures;

internal static class EuresKeywordSearchExpander
{
    public static string[] ExpandSearchTerms(IReadOnlyList<string> userKeywords)
    {
        var expandedTerms = new List<string>();

        foreach (var keyword in userKeywords)
        {
            expandedTerms.AddRange(ExpandSingleKeyword(keyword));
        }

        return expandedTerms
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> ExpandSingleKeyword(string keyword)
    {
        return keyword.Trim().ToLowerInvariant() switch
        {
            ".net" or "dotnet" => [".NET", "dotnet", "C#", "Blazor"],
            _ => [NormalizeForSearch(keyword)]
        };
    }

    public static string NormalizeForSearch(string keyword)
    {
        return keyword.Trim();
    }
}
