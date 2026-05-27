namespace ApplyVault.Api.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// When true, applies pending EF Core migrations during API startup (single-instance deploys).
    /// Set false in Production when migrations run in CI or a dedicated deploy job before traffic.
    /// </summary>
    public bool MigrateAtStartup { get; set; } = true;
}
