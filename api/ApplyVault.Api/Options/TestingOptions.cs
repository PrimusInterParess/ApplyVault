namespace ApplyVault.Api.Options;

public sealed class TestingOptions
{
    public const string SectionName = "Testing";

    public bool UseInMemoryDatabase { get; set; }

    public string InMemoryDatabaseName { get; set; } = "ApplyVaultTests";
}
