using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Umbraco.Community.Licensing.Tool.Data;

/// <summary>The vendor database (ADR-0013). No column holds a key string (PDR-0033).</summary>
internal sealed class ToolDbContext(DbContextOptions<ToolDbContext> options) : DbContext(options)
{
    public DbSet<Setting> Settings => Set<Setting>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<FeatureDefinition> FeatureDefinitions => Set<FeatureDefinition>();

    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    public DbSet<License> Licenses => Set<License>();

    public DbSet<IssueEntry> IssueEntries => Set<IssueEntry>();

    // SQLite cannot order DateTimeOffset. Instants are stored as fixed-width UTC ISO-8601 text
    // (yyyy-MM-ddTHH:mm:ss.fffffffZ): sorts and compares correctly as text and stays readable in any SQLite browser.
    private const string InstantFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    private static string ToText(DateTimeOffset v) => v.UtcDateTime.ToString(InstantFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset FromText(string v) =>
        DateTimeOffset.ParseExact(v, InstantFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    protected override void ConfigureConventions(ModelConfigurationBuilder configuration)
    {
        configuration.Properties<DateTimeOffset>().HaveConversion<InstantConversion>();
        configuration.Properties<DateTimeOffset?>().HaveConversion<NullableInstantConversion>();
        configuration.Properties<SigningKeyRole>().HaveConversion<string>();
        configuration.Properties<ReplacementReason>().HaveConversion<string>();
        configuration.Properties<ReplacementReason?>().HaveConversion<string>();
        configuration.Properties<IssueKind>().HaveConversion<string>();
        configuration.Properties<FeatureType>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Setting>(e =>
        {
            e.ToTable("Settings");
            e.HasKey(x => x.Name);
        });

        model.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.HasKey(x => x.ProductId);
            e.HasMany(x => x.Features).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<FeatureDefinition>(e =>
        {
            e.ToTable("FeatureDefinitions");
            e.HasKey(x => new { x.ProductId, x.Name });
        });

        model.Entity<SigningKey>(e =>
        {
            e.ToTable("SigningKeys");
            e.HasKey(x => x.SigningKeyId);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<License>(e =>
        {
            e.ToTable("Licenses");
            e.HasKey(x => x.Reference);
            e.Property(x => x.Reference).HasMaxLength(10);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.Reference).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<IssueEntry>(e =>
        {
            e.ToTable("IssueEntries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Reference).HasMaxLength(10);
            e.HasOne<SigningKey>().WithMany().HasForeignKey(x => x.SigningKeyId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.OrderRef);
        });
    }

    private sealed class InstantConversion() : ValueConverter<DateTimeOffset, string>(v => ToText(v), v => FromText(v));

    private sealed class NullableInstantConversion() : ValueConverter<DateTimeOffset?, string?>(
        v => v == null ? null : ToText(v.Value),
        v => v == null ? null : FromText(v));
}
