using Microsoft.EntityFrameworkCore;

namespace MyGarage.Infrastructure.Approvals;

// The legacy context owns the table, migrations and all mutations.
public sealed class ApprovalReadDbContext(DbContextOptions<ApprovalReadDbContext> options) : DbContext(options)
{
    internal DbSet<ApprovalReadRow> Requests => Set<ApprovalReadRow>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        var entity = builder.Entity<ApprovalReadRow>();
        entity.ToTable("ApprovalRequests", "dbo", table => table.ExcludeFromMigrations());
        entity.HasKey(x => x.Id);
        entity.Property(x => x.RequestType).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("Read-only context.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only context.");
}

internal sealed class ApprovalReadRow
{
    public long Id { get; set; }
    public string RequestType { get; set; } = null!;
    public long? ServiceOrderDiscountId { get; set; }
    public long? GatePassId { get; set; }
    public long? MaterialReturnId { get; set; }
    public long RequestedBy { get; set; }
    public long? ApproverId { get; set; }
    public string? Status { get; set; }
    public string? Reason { get; set; }
    public string? RejectReason { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
