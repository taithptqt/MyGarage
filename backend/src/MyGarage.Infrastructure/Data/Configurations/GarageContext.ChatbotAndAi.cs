using System;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigureChatbotAndAi(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiForecast>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AI_Forec__3214EC07A43540EC");

            entity.ToTable("AI_Forecasts");

            entity.Property(e => e.ConfidenceScore).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Vehicle).WithMany(p => p.AiForecasts)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AI_Foreca__Vehic__1758727B");
        });

        modelBuilder.Entity<AiFraudMonitor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AI_Fraud__3214EC0761F06F3E");

            entity.ToTable("AI_FraudMonitors");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.EntityType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RiskLevel)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(AiFraudMonitorStatusCode.FLAGGED));
        });

        modelBuilder.Entity<AiKnowledgeBase>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AI_Knowl__3214EC0754A71E47");

            entity.ToTable("AI_KnowledgeBase");

            entity.Property(e => e.IsDynamic).HasDefaultValue(false);
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.SourceUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Topic).HasMaxLength(255);
        });

        modelBuilder.Entity<AiModelConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AI_Model__3214EC07716BF4FE");

            entity.ToTable("AI_ModelConfigs");

            entity.Property(e => e.ApiKey)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ModelName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Provider)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Temperature).HasColumnType("decimal(3, 2)");
        });

        modelBuilder.Entity<AiSmartDispatching>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AI_Smart__3214EC07DC395169");

            entity.ToTable("AI_SmartDispatching");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.EfficiencyScore).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(AiDispatchStatus.GENERATED));

            entity.HasOne(d => d.Branch).WithMany(p => p.AiSmartDispatchings)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AI_SmartD__Branc__184C96B4");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ChatMess__3214EC07A1C55ACB");

            entity.HasIndex(e => e.ClientMessageId, "UX_ChatMessages_ClientMessageId")
                .IsUnique()
                .HasFilter("[ClientMessageId] IS NOT NULL");

            entity.HasIndex(e => new { e.ConversationId, e.Id }, "IX_ChatMessages_ConversationId_Id");

            entity.Property(e => e.AttachmentUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.MessageType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ChatMessageType.TEXT));
            entity.Property(e => e.SenderType)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.ToTable(tb =>
            {
                tb.HasCheckConstraint(
                    "CK_ChatMessages_SenderType",
                    $"[SenderType] IN ('{nameof(ChatSenderType.CUSTOMER)}','{nameof(ChatSenderType.STAFF)}','{nameof(ChatSenderType.SYSTEM)}')");
                tb.HasCheckConstraint(
                    "CK_ChatMessages_MessageType",
                    $"[MessageType] IN ('{nameof(ChatMessageType.TEXT)}','{nameof(ChatMessageType.IMAGE)}','{nameof(ChatMessageType.SYSTEM)}')");
            });

            entity.HasOne(d => d.Conversation).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatMessa__Conve__35DCF99B");
        });

        modelBuilder.Entity<ChatMessageAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ChatMessageAttachments");

            entity.HasIndex(e => new { e.MessageId, e.SortOrder, e.Id }, "IX_ChatMessageAttachments_MessageId_SortOrder_Id");
            entity.HasIndex(e => e.CreatedAt, "IX_ChatMessageAttachments_CreatedAt");

            entity.Property(e => e.AttachmentUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.ContentType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SortOrder).HasDefaultValue(1);

            entity.ToTable(tb =>
            {
                tb.HasCheckConstraint("CK_ChatMessageAttachments_SortOrder", "[SortOrder] >= (1)");
            });

            entity.HasOne(d => d.Message).WithMany(p => p.ChatMessageAttachments)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ChatMessageAttachments_MessageId");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Conversa__3214EC073AA82895");

            entity.HasIndex(e => new { e.BranchId, e.Status, e.LastMessageAt }, "IX_Conversations_Branch_Status_LastMessageAt");
            entity.HasIndex(e => new { e.CustomerId, e.LastMessageAt }, "IX_Conversations_Customer_LastMessageAt");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.LastMessageAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ConversationStatusCode.OPEN));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();

            entity.ToTable(tb =>
            {
                tb.HasCheckConstraint(
                    "CK_Conversations_Status",
                    $"[Status] IN ('{nameof(ConversationStatus.OPEN)}','{nameof(ConversationStatus.WAITING_FOR_STAFF)}','{nameof(ConversationStatus.WAITING_FOR_CUSTOMER)}','{nameof(ConversationStatus.CLOSED)}')");
            });

            entity.HasOne(d => d.Appointment).WithMany()
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_Conversations_AppointmentId");

            entity.HasOne(d => d.AssignedStaff).WithMany()
                .HasForeignKey(d => d.AssignedStaffId)
                .HasConstraintName("FK_Conversations_AssignedStaffId");

            entity.HasOne(d => d.Branch).WithMany()
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_Conversations_BranchId");

            entity.HasOne(d => d.Customer).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Conversat__Custo__442B18F2");

            entity.HasOne(d => d.LastMessageNavigation).WithMany()
                .HasForeignKey(d => d.LastMessageId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Conversations_LastMessageId");

            entity.HasOne(d => d.ServiceOrder).WithMany()
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_Conversations_ServiceOrderId");

            entity.HasOne(d => d.Staff).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("FK__Conversat__Staff__4336F4B9");

            entity.HasOne(d => d.Vehicle).WithMany()
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK_Conversations_VehicleId");
        });

        modelBuilder.Entity<ConversationCustomerRead>(entity =>
        {
            entity.HasKey(e => e.ConversationId).HasName("PK_ConversationCustomerReads");

            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Conversation).WithMany(p => p.ConversationCustomerReads)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ConversationCustomerReads_ConversationId");

            entity.HasOne(d => d.Customer).WithMany()
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ConversationCustomerReads_CustomerId");

            entity.HasOne(d => d.LastReadMessage).WithMany(p => p.ConversationCustomerReads)
                .HasForeignKey(d => d.LastReadMessageId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ConversationCustomerReads_LastReadMessageId");
        });

        modelBuilder.Entity<ConversationStaffRead>(entity =>
        {
            entity.HasKey(e => new { e.ConversationId, e.StaffId }).HasName("PK_ConversationStaffReads");

            entity.HasIndex(e => e.StaffId, "IX_ConversationStaffReads_StaffId");

            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Conversation).WithMany(p => p.ConversationStaffReads)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ConversationStaffReads_ConversationId");

            entity.HasOne(d => d.LastReadMessage).WithMany(p => p.ConversationStaffReads)
                .HasForeignKey(d => d.LastReadMessageId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ConversationStaffReads_LastReadMessageId");

            entity.HasOne(d => d.Staff).WithMany()
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ConversationStaffReads_StaffId");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC0731481BF1");

            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.LinkAction)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SentAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Customer).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Notificat__Custo__0E8E2250");
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC073CB02436");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.TriggerEvent)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.NotificationTemplateCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Notificat__Creat__0F824689");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.NotificationTemplateDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Notificat__Delet__116A8EFB");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.NotificationTemplateUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Notificat__Updat__10766AC2");
        });
    }
}
