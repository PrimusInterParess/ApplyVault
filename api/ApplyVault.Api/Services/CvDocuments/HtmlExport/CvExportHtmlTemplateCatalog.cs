namespace ApplyVault.Api.Services.HtmlExport;

public static class CvExportHtmlTemplateCatalog
{
    public const int MinTemplateId = 1;
    public const int MaxTemplateId = 3;
    public const int ClassicTemplateId = 1;

    public static int NormalizeTemplateId(int templateId) =>
        templateId is 2 or 3 ? templateId : ClassicTemplateId;

    public static bool IsValidTemplateId(int templateId) =>
        templateId is >= MinTemplateId and <= MaxTemplateId;

    public static bool UsesHtmlRenderer(int templateId) =>
        NormalizeTemplateId(templateId) is >= MinTemplateId and <= MaxTemplateId;

    public static string? GetHtmlTemplateFileName(int templateId) =>
        NormalizeTemplateId(templateId) switch
        {
            1 => "template-classic.html",
            2 => "template-modern.html",
            3 => "template-minimal.html",
            _ => null
        };
}
