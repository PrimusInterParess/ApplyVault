namespace ApplyVault.Api.Data;

public sealed class InMemoryDatabaseInitializer : IDatabaseInitializer
{
    public void Initialize(ApplyVaultDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();
    }
}
