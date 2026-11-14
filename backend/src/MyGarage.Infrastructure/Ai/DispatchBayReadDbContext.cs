using Microsoft.EntityFrameworkCore;

namespace MyGarage.Infrastructure.Ai;

// Reads the existing Bays table; GarageContext remains the owner of writes and migrations.
public sealed class DispatchBayReadDbContext(DbContextOptions<DispatchBayReadDbContext> options) : DbContext(options)
{
    internal DbSet<DispatchBayReadRow> Bays => Set<DispatchBayReadRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var entity = builder.Entity<DispatchBayReadRow>();
        entity.ToTable("Bays", "dbo", table => table.ExcludeFromMigrations());
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.Name).HasMaxLength(255);
        entity.Property(x => x.OperationalStatus).HasMaxLength(50).IsUnicode(false);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("Read-only context.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only context.");
}

internal sealed class DispatchBayReadRow
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int SequenceOrder { get; set; }
    public string? OperationalStatus { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDeleted { get; set; }
}
