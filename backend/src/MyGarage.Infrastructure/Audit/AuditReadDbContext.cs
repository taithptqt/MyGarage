using Microsoft.EntityFrameworkCore;

namespace MyGarage.Infrastructure.Audit;

// GarageContext remains the owner of writes and migrations.
public sealed class AuditReadDbContext(DbContextOptions<AuditReadDbContext> options) : DbContext(options)
{
    internal DbSet<AuditReadRow> Logs => Set<AuditReadRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var log = builder.Entity<AuditReadRow>();
        log.ToTable("AuditLogs", "dbo", table => table.ExcludeFromMigrations());
        log.HasKey(x => x.Id);
        log.Property(x => x.Action).HasMaxLength(50).IsUnicode(false);
        log.Property(x => x.IpAddress).HasMaxLength(50).IsUnicode(false);
        log.Property(x => x.SessionId).HasMaxLength(255).IsUnicode(false);
        log.Property(x => x.TableName).HasMaxLength(100).IsUnicode(false);
        log.Property(x => x.UserAgent).HasMaxLength(500);
        log.Property(x => x.UserType).HasMaxLength(20).IsUnicode(false);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("This context is read-only.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This context is read-only.");
}

internal sealed class AuditReadRow
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserType { get; set; } = null!;
    public long? BranchId { get; set; }
    public string Action { get; set; } = null!;
    public string TableName { get; set; } = null!;
    public long RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public DateTime? Timestamp { get; set; }
}
