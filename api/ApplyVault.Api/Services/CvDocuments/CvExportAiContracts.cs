namespace ApplyVault.Api.Services;

public interface ICvExportAiClient
{
    Task<CvStructuredImportResult> PolishAsync(
        IReadOnlyList<CvExportSectionInput> sections,
        CancellationToken cancellationToken = default);
}
