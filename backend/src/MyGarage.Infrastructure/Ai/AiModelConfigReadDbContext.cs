using Microsoft.EntityFrameworkCore;

namespace MyGarage.Infrastructure.Ai;

// Read model only; GarageContext owns writes and schema migrations.
public sealed class AiModelConfigReadDbContext(DbContextOptions<AiModelConfigReadDbContext> options) : DbContext(options)
{
    internal DbSet<AiModelConfigReadRow> Configs => Set<AiModelConfigReadRow>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AiModelConfigReadRow>();
        entity.ToTable("AI_ModelConfigs", "dbo", table => table.ExcludeFromMigrations());
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Provider).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.ApiKey).HasMaxLength(255).IsUnicode(false);
        entity.Property(x => x.ModelName).HasMaxLength(100).IsUnicode(false);
        entity.Property(x => x.Temperature).HasColumnType("decimal(3, 2)");
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("This context is read-only.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This context is read-only.");
}

internal sealed class AiModelConfigReadRow
{
    public long Id { get; set; }
    public string Provider { get; set; } = null!;
    public string? ApiKey { get; set; }
    public string ModelName { get; set; } = null!;
    public int? MaxTokens { get; set; }
    public decimal? Temperature { get; set; }
    public bool? IsActive { get; set; }
}
