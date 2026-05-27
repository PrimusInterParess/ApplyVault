namespace ApplyVault.Api.Services;

internal static class CvPdfProjectsAppendixBuilder
{
    public static byte[] Merge(Stream basePdf, IReadOnlyList<CvPdfProjectSummaryEntry> summaries) =>
        CvPdfProjectsMergeBuilder.MergeAppendixOnly(basePdf, summaries);
}
