using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.Property(e => e.VoucherName).HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ValidDaysOfWeek)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Timezone)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.BudgetLimit).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BudgetUsed).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ApprovalThresholdAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Version).IsRowVersion();

            entity.HasOne(d => d.Campaign)
                .WithMany(p => p.Vouchers)
                .HasForeignKey(d => d.CampaignId)
                .HasConstraintName("FK_Vouchers_VoucherCampaigns_CampaignId");
        });

        modelBuilder.Entity<VoucherCampaign>(entity =>
        {
            entity.ToTable("VoucherCampaigns");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.CampaignCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Objective).HasMaxLength(500);
            entity.Property(e => e.BudgetLimit).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BudgetUsed).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        });

        modelBuilder.Entity<VoucherUsageRule>(entity =>
        {
            entity.ToTable("VoucherUsageRules");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Operator)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsExclude).HasDefaultValue(false);
            entity.Property(e => e.HardBlock).HasDefaultValue(true);
            entity.Property(e => e.Priority).HasDefaultValue(100);

            entity.HasOne(d => d.Voucher)
                .WithMany(p => p.VoucherUsageRules)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<VoucherAssignment>(entity =>
        {
            entity.ToTable("VoucherAssignments");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.AssignmentType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsInclude).HasDefaultValue(true);

            entity.HasOne(d => d.Voucher)
                .WithMany(p => p.VoucherAssignments)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<VoucherConflictRule>(entity =>
        {
            entity.ToTable("VoucherConflictRules");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConflictType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ConflictTarget).HasMaxLength(200);
            entity.Property(e => e.ActionType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MessageCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MessageText).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Priority).HasDefaultValue(100);

            entity.HasOne(d => d.Voucher)
                .WithMany(p => p.VoucherConflictRules)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<VoucherApprovalRequest>(entity =>
        {
            entity.ToTable("VoucherApprovalRequests");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequestType)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RiskLevel)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ImpactAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SlaDeadline).HasColumnName("SLADeadline");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Voucher)
                .WithMany(p => p.VoucherApprovalRequests)
                .HasForeignKey(d => d.VoucherId);

            entity.HasOne(d => d.Redemption)
                .WithMany()
                .HasForeignKey(d => d.RedemptionId)
                .HasConstraintName("FK_VoucherApprovalRequests_VoucherRedemptions_RedemptionId");
        });

        modelBuilder.Entity<VoucherRedemption>(entity =>
        {
            entity.ToTable("VoucherRedemptions");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.VoucherCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ValidationStatus)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.RedemptionStatus)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.FailReasonCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ReverseReason).HasMaxLength(500);
            entity.Property(e => e.EligibleAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BudgetImpact).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AppliedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Voucher)
                .WithMany(p => p.VoucherRedemptions)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ApprovalRequest)
                .WithMany()
                .HasForeignKey(d => d.ApprovalRequestId);
        });

        modelBuilder.Entity<VoucherAuditLog>(entity =>
        {
            entity.ToTable("VoucherAuditLogs");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Action)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ReasonCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CorrelationId)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Voucher)
                .WithMany(p => p.VoucherAuditLogs)
                .HasForeignKey(d => d.VoucherId);
        });

        modelBuilder.Entity<VoucherCodeBatch>(entity =>
        {
            entity.ToTable("VoucherCodeBatches");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.BatchCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SourceType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.FileName).HasMaxLength(260);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TotalCount).HasDefaultValue(0);
            entity.Property(e => e.SuccessCount).HasDefaultValue(0);
            entity.Property(e => e.DuplicateCount).HasDefaultValue(0);
            entity.Property(e => e.InvalidCount).HasDefaultValue(0);

            entity.HasOne(d => d.Voucher)
                .WithMany(p => p.VoucherCodeBatches)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SalaryCoefficient>(entity =>
        {
            entity.ToTable("SalaryCoefficients");

            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.YearsOfService)
                .IsUnique()
                .HasFilter("[YearsOfService] IS NOT NULL AND [IsDeleted] = 0");

            entity.Property(e => e.YearsOfService);
            entity.Property(e => e.CoefficientName).HasMaxLength(120);
            entity.Property(e => e.CoefficientValue).HasColumnType("decimal(8, 3)");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
        });
    }
}
