using ApplyVault.Api.Data;

namespace ApplyVault.Api.Services;

public sealed class ScrapeResultEmailMatcher : IScrapeResultEmailMatcher
{
    public ScrapeResultEntity? FindBestMatch(
        IReadOnlyList<ScrapeResultEntity> candidates,
        GmailMessage message)
    {
        var scored = candidates
            .Select((candidate) => new
            {
                Candidate = candidate,
                Score = ScoreCandidate(candidate, message)
            })
            .Where((item) => item.Score >= 6)
            .OrderByDescending((item) => item.Score)
            .ThenByDescending((item) => item.Candidate.SavedAt)
            .ToArray();

        if (scored.Length == 0)
        {
            return null;
        }

        if (scored.Length > 1 && scored[0].Score - scored[1].Score < 2)
        {
            return null;
        }

        return scored[0].Candidate;
    }

    private static int ScoreCandidate(ScrapeResultEntity candidate, GmailMessage message)
    {
        var searchText = MailTextNormalizer.BuildSearchText(message);
        var score = 0;
        var company = MailTextNormalizer.Normalize(candidate.CompanyName);
        var title = MailTextNormalizer.Normalize(candidate.JobTitle ?? candidate.Title);
        var sender = MailTextNormalizer.Normalize(message.From);

        if (!string.IsNullOrWhiteSpace(company) && searchText.Contains(company, StringComparison.Ordinal))
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(title) && searchText.Contains(title, StringComparison.Ordinal))
        {
            score += 4;
        }
        else
        {
            var titleTokens = title
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where((token) => token.Length >= 4)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var tokenMatches = titleTokens.Count((token) => searchText.Contains(token, StringComparison.Ordinal));

            if (tokenMatches >= 2)
            {
                score += 3;
            }
        }

        var sourceHost = MailTextNormalizer.Normalize(candidate.SourceHostname);

        if (!string.IsNullOrWhiteSpace(sourceHost) && sender.Contains(sourceHost, StringComparison.Ordinal))
        {
            score += 1;
        }

        foreach (var contact in candidate.HiringManagerContacts)
        {
            var value = MailTextNormalizer.Normalize(contact.Value);

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.Contains('@') && sender.Contains(value, StringComparison.Ordinal))
            {
                score += 4;
                break;
            }

            if (searchText.Contains(value, StringComparison.Ordinal) || sender.Contains(value, StringComparison.Ordinal))
            {
                score += 2;
                break;
            }
        }

        if (candidate.SavedAt >= DateTimeOffset.UtcNow.AddDays(-180))
        {
            score += 1;
        }

        return score;
    }
}
