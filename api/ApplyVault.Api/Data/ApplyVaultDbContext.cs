using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Data;

public sealed class ApplyVaultDbContext(DbContextOptions<ApplyVaultDbContext> options) : DbContext(options)
{
    public DbSet<ScrapeResultEntity> ScrapeResults => Set<ScrapeResultEntity>();
    public DbSet<ScrapeResultContactEntity> ScrapeResultContacts => Set<ScrapeResultContactEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScrapeResultEntity>((entity) =>
        {
            entity.HasKey((result) => result.Id);
            entity.Property((result) => result.Title).IsRequired();
            entity.Property((result) => result.Url).IsRequired();
            entity.Property((result) => result.Text).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property((result) => result.ExtractedAt).IsRequired();
            entity.Property((result) => result.SourceHostname).IsRequired();
            entity.Property((result) => result.DetectedPageType).IsRequired();
            entity.HasMany((result) => result.HiringManagerContacts)
                .WithOne()
                .HasForeignKey((contact) => contact.ScrapeResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScrapeResultContactEntity>((entity) =>
        {
            entity.HasKey((contact) => contact.Id);
            entity.Property((contact) => contact.Type).IsRequired();
            entity.Property((contact) => contact.Value).IsRequired();
        });
    }
}
