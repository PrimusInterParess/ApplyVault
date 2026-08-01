using System.Text.Json;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;
using Microsoft.Extensions.Configuration;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ApplyVault.Api.Tests;

/// <summary>
/// Batch harness over diverse PDFs in agent-system/scratch/cv-pdf-formats-2026-08-01/samples.
/// Extract-only by default; set APPLYVAULT_CV_FORMAT_AI=1 to also call Gemini.
/// </summary>
public sealed class CvPdfFormatCorpusTests
{
    private static readonly string ScratchRoot = ResolveScratchRoot();
    private static readonly string SamplesDir = Path.Combine(ScratchRoot, "samples");
    private static readonly string SamplesAiDir = Path.Combine(ScratchRoot, "samples-ai");
    private static readonly string OutDir = Path.Combine(ScratchRoot, "results");

    private sealed record HeaderSignal(string Kind, string Needle);

    [Fact]
    public async Task Corpus_ExtractAndOptionalAi_WritesReport()
    {
        var runAi = string.Equals(
            Environment.GetEnvironmentVariable("APPLYVAULT_CV_FORMAT_AI"),
            "1",
            StringComparison.Ordinal);

        Assert.True(
            Directory.Exists(SamplesDir) || Directory.Exists(SamplesAiDir),
            $"Missing samples dir: {SamplesDir}");
        Directory.CreateDirectory(OutDir);

        var samplesDir = runAi && Directory.Exists(SamplesAiDir) ? SamplesAiDir : SamplesDir;
        if (!Directory.Exists(samplesDir))
        {
            samplesDir = SamplesDir;
        }

        var pdfs = Directory.GetFiles(samplesDir, "*.pdf")
            .OrderBy(static (p) => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.True(pdfs.Length >= 5, $"Expected several sample PDFs, found {pdfs.Length}");

        var catalog = CvSectionCatalogProvider.LoadFromDefaultPath();
        var extractor = new CvPdfFullTextExtractor(catalog);

        GoogleAiCvStructuredImportClient? aiClient = null;
        if (runAi)
        {
            var apiDir = new[]
            {
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "api", "ApplyVault.Api")),
                @"C:\Users\yborisov\Desktop\jobapplications\api\ApplyVault.Api",
            }.First((path) => File.Exists(Path.Combine(path, "appsettings.Development.json")));

            var config = new ConfigurationBuilder()
                .SetBasePath(apiDir)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: false)
                .Build();

            var googleAi = new GoogleAiOptions();
            config.GetSection(GoogleAiOptions.SectionName).Bind(googleAi);
            var importAi = new CvImportAiOptions();
            config.GetSection(CvImportAiOptions.SectionName).Bind(importAi);
            Assert.True(googleAi.Enabled && !string.IsNullOrWhiteSpace(googleAi.ApiKey));

            aiClient = new GoogleAiCvStructuredImportClient(
                new HttpClient(),
                MsOptions.Create(googleAi),
                MsOptions.Create(importAi),
                catalog);
        }

        var rows = new List<object>();
        var failures = new List<string>();

        foreach (var pdfPath in pdfs)
        {
            var name = Path.GetFileName(pdfPath);
            var bytes = await File.ReadAllBytesAsync(pdfPath);
            using var stream = new MemoryStream(bytes);
            var extraction = extractor.Extract(stream);
            var fullText = string.Join('\n', extraction.Lines.Select(static (l) => l.Text));
            var extractPath = Path.Combine(OutDir, Path.GetFileNameWithoutExtension(name) + ".extract.txt");
            await File.WriteAllTextAsync(extractPath, fullText);

            var fallbackSections = extractor.SectionizeForFallback(extraction.Lines);
            var headerSignals = DetectHeaderSignals(fullText);
            var row = new Dictionary<string, object?>
            {
                ["pdf"] = name,
                ["bytes"] = bytes.Length,
                ["quality"] = extraction.Quality.ToString(),
                ["pages"] = extraction.PageCount,
                ["lines"] = extraction.Lines.Count,
                ["chars"] = extraction.CharCount,
                ["sections"] = fallbackSections.Count,
                ["sectionHeadings"] = fallbackSections.Select(static (s) => s.Heading).ToArray(),
                ["headerSignals"] = headerSignals,
                ["firstLines"] = extraction.Lines.Take(8).Select(static (l) => l.Text).ToArray(),
            };

            if (extraction.Quality == CvPdfExtractionQuality.Empty || extraction.Lines.Count == 0)
            {
                failures.Add($"{name}: empty extraction");
                rows.Add(row);
                continue;
            }

            if (runAi && aiClient is not null)
            {
                try
                {
                    var preview = await CvPdfImportPipeline.BuildPreviewAsync(
                        bytes,
                        extractor,
                        aiClient,
                        googleAiEnabled: true);

                    var contact = preview.Sections.FirstOrDefault(static (s) =>
                        s.SectionType.Equals(CvSectionTypes.Contact, StringComparison.OrdinalIgnoreCase));

                    var contactSummary = SummarizeContact(contact);
                    var wireIssues = DescribeContactWireIssues(contact);
                    var missingSignals = headerSignals
                        .Where((signal) =>
                            (signal.Kind is "email" or "phone" or "linkedin" or "github" or "name")
                            && !contactSummary.Any((value) =>
                                value.Contains(signal.Needle, StringComparison.OrdinalIgnoreCase)
                                || signal.Needle.Contains(value, StringComparison.OrdinalIgnoreCase)))
                        .Select((s) => s.Kind + ":" + s.Needle)
                        .ToArray();

                    // Invented contact: values in Contact that are not in extract.
                    var invented = contactSummary
                        .Where((value) =>
                            value.Contains('@', StringComparison.Ordinal)
                            || value.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)
                            || value.Contains("github.com", StringComparison.OrdinalIgnoreCase))
                        .Where((value) => !CvStructuredImportContactGrounding.ContainsLoose(
                            CvStructuredImportContactGrounding.NormalizeForMatch(fullText),
                            value))
                        .ToArray();

                    row["usedAi"] = preview.UsedAi;
                    row["notice"] = preview.Notice;
                    row["resultSections"] = preview.Sections.Select(static (s) => s.Heading + "/" + s.SectionType).ToArray();
                    row["contact"] = contact?.Entries.Select(static (e) => new
                    {
                        e.Title,
                        e.Subtitle,
                        e.Summary,
                        e.Bullets
                    }).ToArray();
                    row["wireIssues"] = wireIssues;
                    row["missingSignals"] = missingSignals;
                    row["inventedSignals"] = invented;

                    var jsonPath = Path.Combine(OutDir, Path.GetFileNameWithoutExtension(name) + ".import.json");
                    await File.WriteAllTextAsync(
                        jsonPath,
                        JsonSerializer.Serialize(row, new JsonSerializerOptions { WriteIndented = true }));

                    if (wireIssues.Length > 0)
                    {
                        failures.Add($"{name}: wire [{string.Join("; ", wireIssues)}]");
                    }
                    else if (invented.Length > 0)
                    {
                        failures.Add($"{name}: invented [{string.Join("; ", invented)}]");
                    }
                    else if (missingSignals.Length > 0)
                    {
                        failures.Add($"{name}: missing [{string.Join("; ", missingSignals)}]");
                    }
                }
                catch (Exception ex)
                {
                    row["aiError"] = ex.GetType().Name + ": " + ex.Message;
                    failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            rows.Add(row);
        }

        var reportPath = Path.Combine(OutDir, runAi ? "corpus-ai-report.json" : "corpus-extract-report.json");
        await File.WriteAllTextAsync(
            reportPath,
            JsonSerializer.Serialize(new
            {
                runAi,
                sampleCount = pdfs.Length,
                failureCount = failures.Count,
                failures,
                rows
            }, new JsonSerializerOptions { WriteIndented = true }));

        // Extract-only mode is informational; AI mode asserts wire/contact coverage.
        if (runAi)
        {
            Assert.True(
                failures.Count == 0,
                $"Corpus gaps ({failures.Count}):\n" + string.Join("\n", failures) + $"\nSee {reportPath}");
        }
        else
        {
            // Always write a short markdown summary for humans.
            var md = new System.Text.StringBuilder();
            md.AppendLine("# CV PDF format corpus — extract");
            md.AppendLine();
            md.AppendLine($"Samples: {pdfs.Length}. Empty/fail: {failures.Count}.");
            md.AppendLine();
            md.AppendLine("| PDF | Quality | Pages | Lines | Chars | Sections | Header signals |");
            md.AppendLine("|---|---|---:|---:|---:|---:|---|");
            foreach (var row in rows)
            {
                var d = (Dictionary<string, object?>)row;
                var signals = d["headerSignals"] is IEnumerable<(string Kind, string Needle)> list
                    ? string.Join(", ", list.Select(static (s) => s.Kind))
                    : "";
                // headerSignals stored as list of tuples serialized oddly — recompute from firstLines if needed
                if (d["headerSignals"] is List<(string Kind, string Needle)> typed)
                {
                    signals = string.Join(", ", typed.Select(static (s) => s.Kind).Distinct());
                }
                else if (d["headerSignals"] is IEnumerable<object> objs)
                {
                    signals = string.Join(", ", objs.Take(8));
                }

                md.AppendLine(
                    $"| `{d["pdf"]}` | {d["quality"]} | {d["pages"]} | {d["lines"]} | {d["chars"]} | {d["sections"]} | {signals} |");
            }

            await File.WriteAllTextAsync(Path.Combine(OutDir, "corpus-extract-summary.md"), md.ToString());
            Assert.True(failures.Count < pdfs.Length, "All samples empty — extractor broken.");
        }
    }

    private static string ResolveScratchRoot()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "agent-system", "scratch", "cv-pdf-formats-2026-08-01")),
            @"C:\Users\yborisov\Desktop\jobapplications\agent-system\scratch\cv-pdf-formats-2026-08-01",
        };

        return candidates.First(Directory.Exists);
    }

    private static List<HeaderSignal> DetectHeaderSignals(string text)
    {
        var signals = new List<HeaderSignal>();
        var lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines.Take(25))
        {
            if (line.Contains('@', StringComparison.Ordinal))
            {
                foreach (var token in line.Split([' ', '|', ';', ',', '\t'], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (token.Contains('@', StringComparison.Ordinal))
                    {
                        signals.Add(new HeaderSignal("email", token.Trim().TrimEnd('.')));
                    }
                }
            }

            if (line.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("linkedin", StringComparison.OrdinalIgnoreCase))
            {
                signals.Add(new HeaderSignal("linkedin", line));
            }

            if (line.Contains("github.com", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("github", StringComparison.OrdinalIgnoreCase))
            {
                signals.Add(new HeaderSignal("github", line));
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\+?\d[\d\s().\-]{6,}\d")
                && !line.Contains('@', StringComparison.Ordinal))
            {
                signals.Add(new HeaderSignal("phone", line));
            }
        }

        foreach (var line in lines.Take(8))
        {
            if (line.Contains('@', StringComparison.Ordinal) || line.Any(char.IsDigit))
            {
                continue;
            }

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length is >= 2 and <= 4
                && words.All(static (w) => w.All(static (ch) => char.IsLetter(ch) || ch is '-' or '\''))
                && CvStructuredImportEntrySupport.LooksLikePlausiblePersonName(line))
            {
                signals.Add(new HeaderSignal("name", line));
                break;
            }
        }

        return signals
            .GroupBy(static (s) => s.Kind + "|" + s.Needle, StringComparer.OrdinalIgnoreCase)
            .Select(static (g) => g.First())
            .ToList();
    }

    private static List<string> SummarizeContact(CvStructuredSectionWriteDto? contact)
    {
        if (contact is null)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var entry in contact.Entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Subtitle))
            {
                values.Add(entry.Subtitle.Trim());
            }

            if (!string.IsNullOrWhiteSpace(entry.Summary))
            {
                values.Add(entry.Summary.Trim());
            }

            foreach (var bullet in entry.Bullets.Where(static (b) => !string.IsNullOrWhiteSpace(b)))
            {
                values.Add(bullet.Trim());
            }
        }

        return values;
    }

    private static string[] DescribeContactWireIssues(CvStructuredSectionWriteDto? contact)
    {
        if (contact is null)
        {
            return [];
        }

        var issues = new List<string>();
        foreach (var entry in contact.Entries)
        {
            if (CvStructuredImportEntrySupport.IsContactNameTitle(entry.Title))
            {
                if (string.IsNullOrWhiteSpace(entry.Subtitle) && !string.IsNullOrWhiteSpace(entry.Summary))
                {
                    issues.Add("Name in summary");
                }

                if (!string.IsNullOrWhiteSpace(entry.Subtitle)
                    && !CvStructuredImportEntrySupport.LooksLikePlausiblePersonName(entry.Subtitle))
                {
                    issues.Add("Name not plausible");
                }
            }
            else if (CvStructuredImportEntrySupport.IsKnownContactChannelLabel(entry.Title))
            {
                var hasBullet = entry.Bullets.Any(static (b) => !string.IsNullOrWhiteSpace(b));
                if (!hasBullet && !string.IsNullOrWhiteSpace(entry.Summary))
                {
                    issues.Add($"{entry.Title} in summary");
                }
            }
        }

        return issues.ToArray();
    }
}
