using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApplyVault.Api.Tests;

// Preserve the application model while adapting SQL Server-specific types for relational tests.
internal sealed class SqliteTestModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            if (string.Equals(property.GetColumnType(), "nvarchar(max)", StringComparison.Ordinal))
            {
                property.SetColumnType("TEXT");
            }

            if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(DateTimeOffset))
            {
                property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
        }
    }
}
