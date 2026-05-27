namespace ApplyVault.Api.Data;

public interface IDatabaseInitializer
{
    void Initialize(ApplyVaultDbContext dbContext);
}
