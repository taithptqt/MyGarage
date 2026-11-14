using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Data.Configurations;

public sealed class ChatbotMessageConfiguration : IEntityTypeConfiguration<ChatbotMessage>
{
    public void Configure(EntityTypeBuilder<ChatbotMessage> entity)
    {
        entity.ToTable("ChatbotMessages", tb =>
        {
            tb.HasCheckConstraint(
                "CK_ChatbotMessages_Role",
                $"[Role] IN ('{nameof(ChatbotMessageRole.USER)}','{nameof(ChatbotMessageRole.ASSISTANT)}','{nameof(ChatbotMessageRole.SYSTEM)}','{nameof(ChatbotMessageRole.TOOL_CALL)}','{nameof(ChatbotMessageRole.TOOL_RESULT)}')");
            tb.HasCheckConstraint(
                "CK_ChatbotMessages_SentimentScore",
                "[SentimentScore] IS NULL OR ([SentimentScore] >= CAST(-1.0000 AS decimal(5,4)) AND [SentimentScore] <= CAST(1.0000 AS decimal(5,4)))");
            tb.HasCheckConstraint(
                "CK_ChatbotMessages_ToolArgs_IsJson",
                "[ToolArgs] IS NULL OR ISJSON([ToolArgs]) = 1");
            tb.HasCheckConstraint(
                "CK_ChatbotMessages_ToolResult_IsJson",
                "[ToolResult] IS NULL OR ISJSON([ToolResult]) = 1");
        });

        entity.HasKey(e => e.Id).HasName("PK_ChatbotMessages");

        entity.HasIndex(e => e.SessionId, "IX_ChatbotMessages_SessionId");
        entity.HasIndex(e => e.CreatedAt, "IX_ChatbotMessages_CreatedAt");
        entity.HasIndex(e => new { e.SessionId, e.CreatedAt }, "IX_ChatbotMessages_SessionId_CreatedAt");

        entity.Property(e => e.Role)
            .HasMaxLength(20)
            .IsUnicode(false);

        entity.Property(e => e.AttachmentUrl).HasMaxLength(2048);
        entity.Property(e => e.IntentName).HasMaxLength(100);
        entity.Property(e => e.SentimentScore).HasColumnType("decimal(5,4)");
        entity.Property(e => e.ToolName)
            .HasMaxLength(100)
            .IsUnicode(false);
        entity.Property(e => e.ToolArgs).HasColumnType("nvarchar(max)");
        entity.Property(e => e.ToolResult).HasColumnType("nvarchar(max)");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

        entity.HasOne(d => d.Session).WithMany(p => p.ChatbotMessages)
            .HasForeignKey(d => d.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ChatbotMessages_ChatbotSessions_SessionId");
    }
}
