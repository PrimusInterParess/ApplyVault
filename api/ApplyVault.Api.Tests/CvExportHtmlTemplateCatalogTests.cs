using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

public sealed class CvExportHtmlTemplateCatalogTests
{
    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 1)]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(99, 1)]
    public void NormalizeTemplateId_keeps_2_and_3_else_Classic(int input, int expected) =>
        Assert.Equal(expected, CvExportHtmlTemplateCatalog.NormalizeTemplateId(input));

    [Fact]
    public void MaxTemplateId_is_3() =>
        Assert.Equal(3, CvExportHtmlTemplateCatalog.MaxTemplateId);

    [Theory]
    [InlineData(1, "template-classic.html")]
    [InlineData(2, "template-modern.html")]
    [InlineData(3, "template-minimal.html")]
    public void GetHtmlTemplateFileName_maps_supported_ids(int templateId, string fileName) =>
        Assert.Equal(fileName, CvExportHtmlTemplateCatalog.GetHtmlTemplateFileName(templateId));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void UsesHtmlRenderer_is_true_for_v1_ids(int templateId) =>
        Assert.True(CvExportHtmlTemplateCatalog.UsesHtmlRenderer(templateId));
}
