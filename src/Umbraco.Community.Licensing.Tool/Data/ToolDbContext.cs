using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Umbraco.Community.Licensing.Tool.Data;

/// <summary>
/// The installation's database (ADR-0013). Created per action and disposed with it (see
/// <see cref="Installation.OpenDb"/>); the connection settings and pragmas come from <see cref="Installation"/>.
/// </summary>
internal sealed class ToolDbContext(DbContextOptions<ToolDbContext> options) : DbContext(options)
{
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<FeatureDefinition> FeatureDefinitions => Set<FeatureDefinition>();
    public DbSet<SigningKeyRecord> SigningKeys => Set<SigningKeyRecord>();
    public DbSet<KeyRelease> KeyReleases => Set<KeyRelease>();
    public DbSet<LicenseRecord> Licenses => Set<LicenseRecord>();
    public DbSet<IssueEntry> IssueEntries => Set<IssueEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Timestamps are UTC (design.md): written as UTC and read back marked UTC, which SQLite would otherwise lose.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Setting>(e =>
        {
            e.ToTable("Settings");
            e.HasKey(x => x.Name);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.HasKey(x => x.ProductId);
        });

        modelBuilder.Entity<FeatureDefinition>(e =>
        {
            e.ToTable("FeatureDefinitions");
            e.HasKey(x => new { x.ProductId, x.Name });
            e.Property(x => x.Type).HasConversion(LowerCase<FeatureType>());
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SigningKeyRecord>(e =>
        {
            e.ToTable("SigningKeys");
            e.HasKey(x => x.SigningKeyId);
            e.Property(x => x.Role).HasConversion(LowerCase<KeyRole>());
            e.Property(x => x.ReplacedReason).HasConversion(LowerCase<ReplacedReason>());
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KeyRelease>(e =>
        {
            e.ToTable("KeyReleases");
            e.HasKey(x => new { x.SigningKeyId, x.ProductId });
            e.HasOne<SigningKeyRecord>().WithMany().HasForeignKey(x => x.SigningKeyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LicenseRecord>(e =>
        {
            e.ToTable("Licenses");
            e.HasKey(x => x.Reference);
            e.Property(x => x.Reference).HasMaxLength(10);
            e.HasIndex(x => x.CustomerEmailKey);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IssueEntry>(e =>
        {
            e.ToTable("IssueEntries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Reference).HasMaxLength(10);
            e.Property(x => x.Kind).HasConversion(LowerCase<IssueKind>());
            e.HasIndex(x => x.OrderRefKey);
            e.HasIndex(x => x.Reference);
            e.HasOne<LicenseRecord>().WithMany().HasForeignKey(x => x.Reference).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SigningKeyRecord>().WithMany().HasForeignKey(x => x.SigningKeyId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static ValueConverter<T, string> LowerCase<T>() where T : struct, Enum =>
        new(v => v.ToString().ToLowerInvariant(), s => Enum.Parse<T>(s, true));

    /// <summary>
    /// Only a value already marked UTC is written. <c>ToUniversalTime</c> would shift an unspecified value by the
    /// machine's offset, so a value from <c>DateTime.Now</c> or a forgotten field is refused instead; take times
    /// from <c>TimeProvider.GetUtcNow().UtcDateTime</c>.
    /// </summary>
    private static DateTime RequireUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new InvalidOperationException($"A timestamp must be UTC, not {value.Kind}: {value:O}.");

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => RequireUtc(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v == null ? null : RequireUtc(v.Value), v => v == null ? null : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
}
