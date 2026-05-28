namespace ApplyVault.Api.Services.HtmlExport;

public static class CvExportHtmlTemplateCatalog
{
    public const int MinTemplateId = 1;
    public const int MaxTemplateId = 5;

    public static bool IsValidTemplateId(int templateId) =>
        templateId is >= MinTemplateId and <= MaxTemplateId;

    public static bool UsesHtmlRenderer(int templateId) => templateId is >= 2 and <= MaxTemplateId;

    public static string? GetHtmlTemplateFileName(int templateId) =>
        templateId switch
        {
            2 => "template-modern.html",
            3 => "template-minimal.html",
            4 => "template-creative.html",
            5 => "template-professional.html",
            _ => null
        };
}
