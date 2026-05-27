using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Data;

public sealed class RelationalDatabaseInitializer : IDatabaseInitializer
{
    public void Initialize(ApplyVaultDbContext dbContext)
    {
        dbContext.Database.Migrate();
    }
}
