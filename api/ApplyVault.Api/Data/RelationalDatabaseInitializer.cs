using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Data;

public sealed class RelationalDatabaseInitializer(IOptions<DatabaseOptions> databaseOptions) : IDatabaseInitializer
{
    public void Initialize(ApplyVaultDbContext dbContext)
    {
        if (!databaseOptions.Value.MigrateAtStartup)
        {
            return;
        }

        dbContext.Database.Migrate();
    }
}
