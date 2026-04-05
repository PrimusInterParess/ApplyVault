using System.Globalization;
using System.Text.RegularExpressions;

namespace ApplyVault.Api.Services;

public sealed class InterviewScheduleExtractor : IInterviewScheduleExtractor
{
    private static readonly string[] LocationPrefixes =
    [
        "location:",
        "lokation:",
        "sted:",
        "adresse:"
    ];

    private static readonly Dictionary<string, int> MonthNumbers = new(StringComparer.Ordinal)
    {
        ["jan"] = 1,
        ["january"] = 1,
        ["januar"] = 1,
        ["feb"] = 2,
        ["february"] = 2,
        ["februar"] = 2,
        ["mar"] = 3,
        ["march"] = 3,
        ["marts"] = 3,
        ["apr"] = 4,
        ["april"] = 4,
        ["may"] = 5,
        ["maj"] = 5,
        ["jun"] = 6,
        ["june"] = 6,
        ["juni"] = 6,
        ["jul"] = 7,
        ["july"] = 7,
        ["juli"] = 7,
        ["aug"] = 8,
        ["august"] = 8,
        ["sep"] = 9,
        ["september"] = 9,
        ["oct"] = 10,
        ["october"] = 10,
        ["okt"] = 10,
        ["oktober"] = 10,
        ["nov"] = 11,
        ["november"] = 11,
        ["dec"] = 12,
        ["december"] = 12
    };

    private static readonly Regex[] InterviewDateRegexes =
    [
        new(
            @"\b(?:(?:mon(?:day)?|tues(?:day)?|wednes(?:day)?|thurs(?:day)?|fri(?:day)?|satur(?:day)?|sun(?:day)?|mandag|tirsdag|onsdag|torsdag|fredag|loerdag|soendag)\s*,?\s+)?(?<month>jan(?:uary)?|januar|feb(?:ruary)?|februar|mar(?:ch)?|marts|apr(?:il)?|may|maj|jun(?:e)?|juni|jul(?:y)?|juli|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|okt(?:ober)?|nov(?:ember)?|dec(?:ember)?)\s+(?<day>\d{1,2})(?:st|nd|rd|th)?(?:[,\.\s]+(?<year>\d{4}))?(?:\s*(?:at|kl\.?)\s*|\s*,\s*|\s+|[\s\u00b7]+)(?<start>\d{1,2}(?:(?::|\.)\d{2})?\s*(?:am|pm)?)(?:\s*(?:-|to|til)\s*(?<end>\d{1,2}(?:(?::|\.)\d{2})?\s*(?:am|pm)?))?(?:\s*(?<tz>et|est|edt|ct|cst|cdt|mt|mst|mdt|pt|pst|pdt|utc|gmt|cet|cest))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(
            @"\b(?:(?:mon(?:day)?|tues(?:day)?|wednes(?:day)?|thurs(?:day)?|fri(?:day)?|satur(?:day)?|sun(?:day)?|mandag|tirsdag|onsdag|torsdag|fredag|loerdag|soendag)\s*,?\s+)?(?:den\s+)?(?<day>\d{1,2})\.?\s+(?<month>jan(?:uary)?|januar|feb(?:ruary)?|februar|mar(?:ch)?|marts|apr(?:il)?|may|maj|jun(?:e)?|juni|jul(?:y)?|juli|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|okt(?:ober)?|nov(?:ember)?|dec(?:ember)?)(?:\s+(?<year>\d{4}))?(?:\s*(?:at|kl\.?)\s*|\s+)(?<start>\d{1,2}(?:(?::|\.)\d{2})?\s*(?:am|pm)?)(?:\s*(?:-|to|til)\s*(?<end>\d{1,2}(?:(?::|\.)\d{2})?\s*(?:am|pm)?))?(?:\s*(?<tz>et|est|edt|ct|cst|cdt|mt|mst|mdt|pt|pst|pdt|utc|gmt|cet|cest))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(
            @"\b(?<day>\d{1,2})[\/\.-](?<monthNumber>\d{1,2})[\/\.-](?<year>\d{4})(?:\s*(?:at|kl\.?)\s*|\s+)(?<start>\d{1,2}(?:(?::|\.)\d{2})?\s*(?:am|pm)?)(?:\s*(?:-|to|til)\s*(?<end>\d{1,2}(?:(?::|\.)\d{2})?\s*(?:am|pm)?))?(?:\s*(?<tz>et|est|edt|ct|cst|cdt|mt|mst|mdt|pt|pst|pdt|utc|gmt|cet|cest))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)
    ];

    public bool TryExtractSchedule(GmailMessage message, out EmailDrivenInterviewSchedule? schedule)
    {
        var combined = $"{message.Subject}\n{message.BodyText}\n{message.Snippet}";
        var normalizedCombined = MailTextNormalizer.Normalize(combined);
        var location = ExtractLocation(combined);

        foreach (var regex in InterviewDateRegexes)
        {
            var match = regex.Match(normalizedCombined);

            if (match.Success && TryCreateInterviewSchedule(match, combined, message.ReceivedAt, location, out schedule))
            {
                return true;
            }
        }

        schedule = null;
        return false;
    }

    private static bool TryCreateInterviewSchedule(
        Match match,
        string input,
        DateTimeOffset receivedAt,
        string? location,
        out EmailDrivenInterviewSchedule? schedule)
    {
        var year = match.Groups["year"].Success && int.TryParse(match.Groups["year"].Value, CultureInfo.InvariantCulture, out var parsedYear)
            ? parsedYear
            : receivedAt.Year;

        int month;

        if (match.Groups["monthNumber"].Success)
        {
            if (!int.TryParse(match.Groups["monthNumber"].Value, CultureInfo.InvariantCulture, out month))
            {
                schedule = null;
                return false;
            }
        }
        else if (!TryResolveMonthNumber(match.Groups["month"].Value, out month))
        {
            schedule = null;
            return false;
        }

        if (!int.TryParse(match.Groups["day"].Value, CultureInfo.InvariantCulture, out var day))
        {
            schedule = null;
            return false;
        }

        var endValue = match.Groups["end"].Success ? match.Groups["end"].Value : null;

        if (!TryParseTime(match.Groups["start"].Value, endValue, out var startTime))
        {
            schedule = null;
            return false;
        }

        DateTime startLocal;

        try
        {
            startLocal = new DateTime(year, month, day, startTime.Hours, startTime.Minutes, 0, DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException)
        {
            schedule = null;
            return false;
        }

        var endLocal = startLocal.AddMinutes(30);

        if (match.Groups["end"].Success && TryParseTime(match.Groups["end"].Value, null, out var endTime))
        {
            endLocal = new DateTime(year, month, day, endTime.Hours, endTime.Minutes, 0, DateTimeKind.Unspecified);

            if (endLocal <= startLocal)
            {
                endLocal = endLocal.AddHours(1);
            }
        }

        var zone = ResolveTimeZone(match.Groups["tz"].Value, input);
        schedule = new EmailDrivenInterviewSchedule(
            TimeZoneInfo.ConvertTimeToUtc(startLocal, zone),
            TimeZoneInfo.ConvertTimeToUtc(endLocal, zone),
            zone.Id,
            location);
        return true;
    }

    private static TimeZoneInfo ResolveTimeZone(string abbreviation, string input)
    {
        var value = abbreviation.Trim().ToUpperInvariant();
        var explicitZoneId = ExtractTimeZoneId(input);

        if (!string.IsNullOrWhiteSpace(explicitZoneId))
        {
            return FindTimeZone(explicitZoneId);
        }

        var zoneId = value switch
        {
            "ET" or "EST" or "EDT" => "Eastern Standard Time",
            "CT" or "CST" or "CDT" => "Central Standard Time",
            "MT" or "MST" or "MDT" => "Mountain Standard Time",
            "PT" or "PST" or "PDT" => "Pacific Standard Time",
            "CET" or "CEST" => "W. Europe Standard Time",
            "UTC" or "GMT" => "UTC",
            _ => TimeZoneInfo.Local.Id
        };

        return FindTimeZone(zoneId);
    }

    private static string? ExtractLocation(string input)
    {
        var lines = input
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where((line) => line.Length > 0)
            .ToArray();

        foreach (var line in lines)
        {
            var normalizedLine = MailTextNormalizer.Normalize(line);

            if (LocationPrefixes.Any((prefix) => normalizedLine.StartsWith(prefix, StringComparison.Ordinal)))
            {
                var separatorIndex = line.IndexOf(':');

                if (separatorIndex >= 0 && separatorIndex < line.Length - 1)
                {
                    return MailTextNormalizer.Truncate(line[(separatorIndex + 1)..].Trim(), 256);
                }
            }
        }

        return null;
    }

    private static bool TryResolveMonthNumber(string month, out int monthNumber) =>
        MonthNumbers.TryGetValue(MailTextNormalizer.Normalize(month), out monthNumber);

    private static bool TryParseTime(string value, string? fallbackMeridiemSource, out TimeSpan time)
    {
        var normalized = Regex.Replace(value.Trim().Replace('.', ':'), @"\s+", " ");

        if (!Regex.IsMatch(normalized, @"(?:am|pm)\b", RegexOptions.IgnoreCase) &&
            !string.IsNullOrWhiteSpace(fallbackMeridiemSource))
        {
            var fallbackMeridiemMatch = Regex.Match(fallbackMeridiemSource, @"(?<meridiem>am|pm)\b", RegexOptions.IgnoreCase);

            if (fallbackMeridiemMatch.Success)
            {
                normalized = $"{normalized} {fallbackMeridiemMatch.Groups["meridiem"].Value}";
            }
        }

        if (DateTime.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault,
                out var parsed))
        {
            time = parsed.TimeOfDay;
            return true;
        }

        time = default;
        return false;
    }

    private static string? ExtractTimeZoneId(string input)
    {
        var lines = input
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where((line) => line.Length > 0)
            .ToArray();

        foreach (var line in lines)
        {
            if (line.StartsWith("Time zone:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Timezone:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Tidszone:", StringComparison.OrdinalIgnoreCase))
            {
                var separatorIndex = line.IndexOf(':');

                if (separatorIndex >= 0 && separatorIndex < line.Length - 1)
                {
                    return line[(separatorIndex + 1)..].Trim();
                }
            }
        }

        return null;
    }

    private static TimeZoneInfo FindTimeZone(string zoneId)
    {
        var mappedZoneId = zoneId.Trim() switch
        {
            "Europe/Copenhagen" => "W. Europe Standard Time",
            _ => zoneId.Trim()
        };

        return TimeZoneInfo.FindSystemTimeZoneById(mappedZoneId);
    }
}
