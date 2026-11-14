using Microsoft.EntityFrameworkCore;

namespace MyGarage.Infrastructure.Ai;

// The legacy GarageContext owns this table and all writes and migrations.
public sealed class AiKnowledgeReadDbContext(DbContextOptions<AiKnowledgeReadDbContext> options) : DbContext(options)
{
    internal DbSet<AiKnowledgeReadRow> Knowledge => Set<AiKnowledgeReadRow>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AiKnowledgeReadRow>();
        entity.ToTable("AI_KnowledgeBase", "dbo", table => table.ExcludeFromMigrations());
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Topic).HasMaxLength(255);
        entity.Property(x => x.SourceUrl).HasMaxLength(500).IsUnicode(false);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("This context is read-only.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This context is read-only.");
}

internal sealed class AiKnowledgeReadRow
{
    public long Id { get; set; }
    public string Topic { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? SourceUrl { get; set; }
    public bool? IsDynamic { get; set; }
    public DateTime? LastUpdated { get; set; }
}
