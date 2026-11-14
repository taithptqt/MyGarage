using Microsoft.EntityFrameworkCore;

namespace MyGarage.Infrastructure.GatePasses;

// Read model only; the legacy context remains responsible for writes and schema.
public sealed class GatePassReadDbContext(DbContextOptions<GatePassReadDbContext> options) : DbContext(options)
{
    internal DbSet<GatePassReadRow> GatePasses => Set<GatePassReadRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GatePassReadRow>();
        entity.ToTable("GatePass", "dbo", table => table.ExcludeFromMigrations());
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ClearanceType).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.QrCode).HasMaxLength(100).IsUnicode(false);
        entity.Property(x => x.OldPartsPackingListUrl).HasMaxLength(500).IsUnicode(false);
        entity.Property(x => x.Status).HasMaxLength(50).IsUnicode(false);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("This context is read-only.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This context is read-only.");
}

internal sealed class GatePassReadRow
{
    public long Id { get; set; }
    public long ServiceOrderId { get; set; }
    public string ClearanceType { get; set; } = null!;
    public string QrCode { get; set; } = null!;
    public long IssuedBy { get; set; }
    public long? ApprovedBy { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? OldPartsPackingListUrl { get; set; }
    public bool? IsOldPartsChecked { get; set; }
    public string Status { get; set; } = null!;
    public long? ScannedBy { get; set; }
    public DateTime? SecurityCheckedAt { get; set; }
    public DateTime? PassedAt { get; set; }
}
