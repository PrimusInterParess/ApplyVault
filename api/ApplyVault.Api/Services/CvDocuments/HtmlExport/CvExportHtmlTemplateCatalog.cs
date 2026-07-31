namespace ApplyVault.Api.Services.HtmlExport;

public static class CvExportHtmlTemplateCatalog
{
    public const int MaxTemplateId = 3;
    public const int ModernTemplateId = 2;
    public const int MinimalTemplateId = 3;
    public const int DefaultTemplateId = ModernTemplateId;

    public static int NormalizeTemplateId(int templateId) =>
        templateId is 2 or 3 ? templateId : DefaultTemplateId;

    public static bool IsValidTemplateId(int templateId) =>
        templateId is 2 or 3;

    public static bool UsesHtmlRenderer(int templateId) =>
        IsValidTemplateId(NormalizeTemplateId(templateId));

    public static string? GetHtmlTemplateFileName(int templateId) =>
        NormalizeTemplateId(templateId) switch
        {
            2 => "template-modern.html",
            3 => "template-minimal.html",
            _ => null
        };

    public static string GetDisplayName(int templateId) =>
        NormalizeTemplateId(templateId) switch
        {
            3 => "Minimal",
            _ => "Modern"
        };
}
