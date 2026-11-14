using Microsoft.EntityFrameworkCore;

namespace MyGarage.Infrastructure.Ai;

public sealed class ForecastReadDbContext(DbContextOptions<ForecastReadDbContext> options) : DbContext(options)
{
    internal DbSet<ForecastReadRow> Forecasts => Set<ForecastReadRow>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ForecastReadRow>();
        entity.ToTable("AI_Forecasts", "dbo", table => table.ExcludeFromMigrations());
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Type).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.ConfidenceScore).HasColumnType("decimal(5, 2)");
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("Read-only context.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only context.");
}

internal sealed class ForecastReadRow
{
    public long Id { get; set; }
    public long VehicleId { get; set; }
    public string Type { get; set; } = null!;
    public DateOnly PredictedDate { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public DateTime? CreatedAt { get; set; }
}
