using Microsoft.EntityFrameworkCore;

namespace MyGarage.Infrastructure.Payments;

// This context maps the existing table for reads only. GarageContext owns migrations and writes.
public sealed class PaymentReadDbContext(DbContextOptions<PaymentReadDbContext> options) : DbContext(options)
{
    internal DbSet<PaymentReadRow> Payments => Set<PaymentReadRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PaymentReadRow>();
        entity.ToTable("Payments", "dbo", table => table.ExcludeFromMigrations());
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Amount).HasColumnType("decimal(18, 2)");
        entity.Property(x => x.PayerType).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.Status).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.TransactionCode).HasMaxLength(100).IsUnicode(false);
        entity.Property(x => x.Type).HasMaxLength(50).IsUnicode(false);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("This context is read-only.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This context is read-only.");
}

internal sealed class PaymentReadRow
{
    public long Id { get; set; }
    public long? ShiftClosingId { get; set; }
    public long? CashierId { get; set; }
    public long? ServiceOrderId { get; set; }
    public long? InvoiceId { get; set; }
    public long? RetailOrderId { get; set; }
    public string? PayerType { get; set; }
    public long? PayerId { get; set; }
    public long MethodId { get; set; }
    public decimal Amount { get; set; }
    public string? TransactionCode { get; set; }
    public string Type { get; set; } = null!;
    public string? Status { get; set; }
    public long? ReversedPaymentId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
}
