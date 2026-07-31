using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

public sealed class CvExportHtmlTemplateCatalogTests
{
    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(1, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(0, 2)]
    [InlineData(-1, 2)]
    [InlineData(99, 2)]
    public void NormalizeTemplateId_keeps_2_and_3_else_Modern(int input, int expected) =>
        Assert.Equal(expected, CvExportHtmlTemplateCatalog.NormalizeTemplateId(input));

    [Fact]
    public void MaxTemplateId_is_3() =>
        Assert.Equal(3, CvExportHtmlTemplateCatalog.MaxTemplateId);

    [Fact]
    public void DefaultTemplateId_is_Modern() =>
        Assert.Equal(2, CvExportHtmlTemplateCatalog.DefaultTemplateId);

    [Theory]
    [InlineData(2, "template-modern.html")]
    [InlineData(3, "template-minimal.html")]
    [InlineData(1, "template-modern.html")]
    public void GetHtmlTemplateFileName_maps_supported_ids(int templateId, string fileName) =>
        Assert.Equal(fileName, CvExportHtmlTemplateCatalog.GetHtmlTemplateFileName(templateId));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void UsesHtmlRenderer_is_true_for_normalized_ids(int templateId) =>
        Assert.True(CvExportHtmlTemplateCatalog.UsesHtmlRenderer(templateId));

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void IsValidTemplateId_allows_only_Modern_and_Minimal(int templateId, bool expected) =>
        Assert.Equal(expected, CvExportHtmlTemplateCatalog.IsValidTemplateId(templateId));

    [Theory]
    [InlineData(1, "Modern")]
    [InlineData(2, "Modern")]
    [InlineData(3, "Minimal")]
    [InlineData(99, "Modern")]
    public void GetDisplayName_maps_supported_ids(int templateId, string displayName) =>
        Assert.Equal(displayName, CvExportHtmlTemplateCatalog.GetDisplayName(templateId));
}
