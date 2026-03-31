using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface IScrapeResultStore
{
    IReadOnlyCollection<SavedScrapeResult> GetAll();

    SavedScrapeResult? GetById(Guid id);

    SavedScrapeResult Save(ScrapeResultDto result);
}
