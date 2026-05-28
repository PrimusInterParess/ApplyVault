namespace ApplyVault.Api.Options;

public sealed class CvHtmlExportOptions
{
    public const string SectionName = "CvHtmlExport";

    public bool EnableHtmlTemplates { get; set; }

    public string TemplatesSubfolder { get; set; } = "cv-export-templates";

    public string? ChromiumExecutablePath { get; set; }

    public int MaxConcurrentExports { get; set; } = 2;
}
