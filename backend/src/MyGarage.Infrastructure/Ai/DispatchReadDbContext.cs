using Microsoft.EntityFrameworkCore;

namespace MyGarage.Infrastructure.Ai;

public sealed class DispatchReadDbContext(DbContextOptions<DispatchReadDbContext> options) : DbContext(options)
{
    internal DbSet<DispatchReadRow> Rows => Set<DispatchReadRow>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DispatchReadRow>();
        entity.ToTable("AI_SmartDispatching", "dbo", table => table.ExcludeFromMigrations());
        entity.HasKey(x => x.Id);
        entity.Property(x => x.EfficiencyScore).HasColumnType("decimal(5, 2)");
        entity.Property(x => x.Status).HasMaxLength(50).IsUnicode(false);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("Read-only context.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only context.");
}

internal sealed class DispatchReadRow
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public DateOnly Date { get; set; }
    public string? Recommendation { get; set; }
    public decimal? EfficiencyScore { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}
