using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvExportLayoutResolverTests
{
    [Fact]
    public void ResolveDocument_with_photo_forces_top_right()
    {
        var layout = CvExportLayoutResolver.ResolveDocument(
            new CvExportLayoutDocumentStyle("wide", "hidden", "large"),
            hasProfilePhoto: true);

        Assert.Equal("topRight", layout.PhotoPlacement);
        Assert.Equal(100, layout.PhotoSizePoints);
    }

    [Fact]
    public void ResolveSection_first_section_has_no_extra_space_before()
    {
        var layout = CvExportLayoutResolver.ResolveSection(CvSectionTypes.Summary, isFirstSection: true);

        Assert.Equal(0, layout.SpaceBefore);
        Assert.True(layout.DrawHeadingRule);
    }
}
