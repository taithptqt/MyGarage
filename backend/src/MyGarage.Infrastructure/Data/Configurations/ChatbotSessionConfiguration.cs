using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Data.Configurations;

public sealed class ChatbotSessionConfiguration : IEntityTypeConfiguration<ChatbotSession>
{
    public void Configure(EntityTypeBuilder<ChatbotSession> entity)
    {
        entity.ToTable("ChatbotSessions", tb =>
        {
            tb.HasCheckConstraint(
                "CK_ChatbotSessions_Status",
                $"[Status] IN ('{nameof(ChatbotSessionStatus.ACTIVE)}','{nameof(ChatbotSessionStatus.IDLE)}','{nameof(ChatbotSessionStatus.HANDED_OFF)}','{nameof(ChatbotSessionStatus.EXPIRED)}','{nameof(ChatbotSessionStatus.CLOSED)}','{nameof(ChatbotSessionStatus.ARCHIVED)}')");
            tb.HasCheckConstraint(
                "CK_ChatbotSessions_SessionToken_NotEmpty",
                "LEN([SessionToken]) > 0");
            tb.HasCheckConstraint(
                "CK_ChatbotSessions_IntentState_IsJson",
                "[IntentState] IS NULL OR ISJSON([IntentState]) = 1");
            tb.HasCheckConstraint(
                "CK_ChatbotSessions_HandoffSummary_IsJson",
                "[HandoffSummary] IS NULL OR ISJSON([HandoffSummary]) = 1");
        });

        entity.HasKey(e => e.Id).HasName("PK_ChatbotSessions");

        entity.HasIndex(e => e.SessionToken, "UX_ChatbotSessions_SessionToken").IsUnique();
        entity.HasIndex(e => e.CustomerId, "IX_ChatbotSessions_CustomerId");
        entity.HasIndex(e => e.BranchId, "IX_ChatbotSessions_BranchId");
        entity.HasIndex(e => e.Status, "IX_ChatbotSessions_Status");
        entity.HasIndex(e => e.LastActivityAt, "IX_ChatbotSessions_LastActivityAt");
        entity.HasIndex(e => e.CreatedAt, "IX_ChatbotSessions_CreatedAt");
        entity.HasIndex(e => new { e.CustomerId, e.Status, e.LastActivityAt }, "IX_ChatbotSessions_CustomerId_Status_LastActivityAt");

        entity.Property(e => e.SessionToken)
            .HasMaxLength(128)
            .IsUnicode(false);

        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue(nameof(ChatbotSessionStatus.ACTIVE));

        entity.Property(e => e.LastActivityAt).HasDefaultValueSql("(sysutcdatetime())");
        entity.Property(e => e.CurrentIntent).HasMaxLength(100);
        entity.Property(e => e.IntentState).HasColumnType("nvarchar(max)");
        entity.Property(e => e.HandoffSummary).HasColumnType("nvarchar(max)");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

        entity.HasOne(d => d.Customer).WithMany()
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ChatbotSessions_Customers_CustomerId");

        entity.HasOne(d => d.Branch).WithMany()
            .HasForeignKey(d => d.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ChatbotSessions_Branches_BranchId");

        entity.HasOne(d => d.ContextVehicle).WithMany()
            .HasForeignKey(d => d.ContextVehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ChatbotSessions_Vehicles_ContextVehicleId");

        entity.HasOne(d => d.ContextServiceCategory).WithMany()
            .HasForeignKey(d => d.ContextServiceCategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ChatbotSessions_ServiceCategories_ContextServiceCategoryId");

        entity.HasOne(d => d.HandoffConversation).WithMany()
            .HasForeignKey(d => d.HandoffConversationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ChatbotSessions_Conversations_HandoffConversationId");
    }
}
