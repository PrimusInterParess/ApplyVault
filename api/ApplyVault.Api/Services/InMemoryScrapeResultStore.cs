using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface IScrapeResultStore
{
    IReadOnlyCollection<SavedScrapeResult> GetAll();

    SavedScrapeResult? GetById(Guid id);

    SavedScrapeResult Save(ScrapeResultDto result);
}

public sealed class InMemoryScrapeResultStore : IScrapeResultStore
{
    private readonly List<SavedScrapeResult> _results = [];
    private readonly Lock _lock = new();

    public IReadOnlyCollection<SavedScrapeResult> GetAll()
    {
        lock (_lock)
        {
            return _results.ToArray();
        }
    }

    public SavedScrapeResult? GetById(Guid id)
    {
        lock (_lock)
        {
            return _results.FirstOrDefault((result) => result.Id == id);
        }
    }

    public SavedScrapeResult Save(ScrapeResultDto result)
    {
        var savedResult = new SavedScrapeResult(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            result
        );

        lock (_lock)
        {
            _results.Add(savedResult);
        }

        return savedResult;
    }
}
