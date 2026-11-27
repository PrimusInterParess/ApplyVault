using System.Security.Cryptography;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ApplyVault.Api.Data;

namespace ApplyVault.Api.Services.JobRequirements;

internal static partial class JobRequirementsSource
{
    public const int MaxDescriptionCharacters = 100_000;

    public static string Description(ScrapeResultEntity job) =>
        (string.IsNullOrWhiteSpace(job.JobDescriptionOverride) ? job.JobDescription : job.JobDescriptionOverride)?.Trim() ?? "";

    public static string Fingerprint(string description) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(description)));

    public static string NormalizeWhitespace(string value) => Whitespace().Replace(value, " ").Trim();

    public static bool HasUsableDescription(string description) =>
        !string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(HtmlTags().Replace(description, " ")));

    public static void Synchronize(ScrapeResultEntity job)
    {
        var description = Description(job);
        var fingerprint = Fingerprint(description);
        if (job.RequirementsRevision > 0 && string.Equals(job.RequirementsSourceHash, fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        job.RequirementsRevision++;
        job.RequirementsSourceHash = fingerprint;
        job.RequirementsStatus = !HasUsableDescription(description)
            ? JobRequirementsStatus.DescriptionNeeded
            : JobRequirementsStatus.Pending;
        job.RequirementsJson = null;
        job.RequirementsAttempts = 0;
        job.RequirementsNextAttemptUtc = null;
        job.RequirementsLeaseId = null;
        job.RequirementsLeaseExpiresUtc = null;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex HtmlTags();
}
