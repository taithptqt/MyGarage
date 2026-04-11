using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Data.Configurations;

public sealed class ChatbotActionLogConfiguration : IEntityTypeConfiguration<ChatbotActionLog>
{
    public void Configure(EntityTypeBuilder<ChatbotActionLog> entity)
    {
        entity.ToTable("ChatbotActionLogs", tb =>
        {
            tb.HasCheckConstraint(
                "CK_ChatbotActionLogs_Status",
                $"[Status] IN ('{nameof(ChatbotActionStatus.PENDING)}','{nameof(ChatbotActionStatus.SUCCESS)}','{nameof(ChatbotActionStatus.FAILED)}','{nameof(ChatbotActionStatus.SKIPPED)}')");
            tb.HasCheckConstraint(
                "CK_ChatbotActionLogs_ActionPayload_IsJson",
                "[ActionPayload] IS NULL OR ISJSON([ActionPayload]) = 1");
        });

        entity.HasKey(e => e.Id).HasName("PK_ChatbotActionLogs");

        entity.HasIndex(e => e.SessionId, "IX_ChatbotActionLogs_SessionId");
        entity.HasIndex(e => e.CreatedAt, "IX_ChatbotActionLogs_CreatedAt");
        entity.HasIndex(e => new { e.SessionId, e.CreatedAt }, "IX_ChatbotActionLogs_SessionId_CreatedAt");

        entity.Property(e => e.ActionType)
            .HasMaxLength(50)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue(nameof(ChatbotActionStatus.SUCCESS));
        entity.Property(e => e.ActionPayload).HasColumnType("nvarchar(max)");
        entity.Property(e => e.ErrorCode)
            .HasMaxLength(100)
            .IsUnicode(false);
        entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

        entity.HasOne(d => d.Session).WithMany(p => p.ChatbotActionLogs)
            .HasForeignKey(d => d.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ChatbotActionLogs_ChatbotSessions_SessionId");
    }
}
