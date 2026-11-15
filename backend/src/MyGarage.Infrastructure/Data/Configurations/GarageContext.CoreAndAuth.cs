using System;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigureCoreAndAuth(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Approval__3214EC07BCDC6900");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.RequestType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ApprovalRequestStatusCode.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Approver).WithMany(p => p.ApprovalRequestApprovers)
                .HasForeignKey(d => d.ApproverId)
                .HasConstraintName("FK__ApprovalR__Appro__22CA2527");

            entity.HasOne(d => d.GatePass).WithMany(p => p.ApprovalRequests)
                .HasForeignKey(d => d.GatePassId)
                .HasConstraintName("FK__ApprovalR__GateP__1FEDB87C");

            entity.HasOne(d => d.MaterialReturn).WithMany(p => p.ApprovalRequests)
                .HasForeignKey(d => d.MaterialReturnId)
                .HasConstraintName("FK__ApprovalR__Mater__20E1DCB5");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.ApprovalRequestRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ApprovalR__Reque__21D600EE");

            entity.HasOne(d => d.ServiceOrderDiscount).WithMany(p => p.ApprovalRequests)
                .HasForeignKey(d => d.ServiceOrderDiscountId)
                .HasConstraintName("FK__ApprovalR__Servi__1EF99443");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AuditLog__3214EC0770616275");

            entity.HasIndex(e => new { e.TableName, e.RecordId }, "IX_AuditLogs_TableName_RecordId");

            entity.Property(e => e.Action)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SessionId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TableName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.UserType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Branch).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK__AuditLogs__Branc__23BE4960");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Branches__3214EC0712918BB8");

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Hotline)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Lat).HasColumnType("decimal(10, 8)");
            entity.Property(e => e.Lng).HasColumnType("decimal(11, 8)");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(BranchStatusCode.ACTIVE));
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.BranchCreatedByNavigations).HasForeignKey(d => d.CreatedBy);

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.BranchDeletedByNavigations).HasForeignKey(d => d.DeletedBy);

            entity.HasOne(d => d.Manager).WithMany(p => p.BranchManagers).HasForeignKey(d => d.ManagerId);

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.BranchUpdatedByNavigations).HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<BranchQuotationApprovalPolicy>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_BranchQuotationApprovalPolicies");

            entity.HasIndex(e => e.BranchId, "UQ_BranchQuotationApprovalPolicies_BranchId").IsUnique();

            entity.Property(e => e.MaxDiscountPercentWithoutManager)
                .HasColumnType("decimal(5, 2)")
                .HasDefaultValue(5m);
            entity.Property(e => e.HighValueThreshold)
                .HasColumnType("decimal(18, 2)")
                .HasDefaultValue(50000000m);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchQuotationApprovalPolicies)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_BranchQuotationApprovalPolicies_Branches");
        });

        modelBuilder.Entity<BranchException>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BranchEx__3214EC0719CB714B");

            entity.HasIndex(e => new { e.BranchId, e.ExceptionDate }, "UQ_BranchId_ExceptionDate").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsClosed).HasDefaultValue(true);
            entity.Property(e => e.Reason).HasMaxLength(255);

            entity.HasOne(d => d.ApprovedByStaff).WithMany(p => p.BranchExceptionApprovedByStaffs)
                .HasForeignKey(d => d.ApprovedByStaffId)
                .HasConstraintName("FK__BranchExc__Appro__2F2FFC0C");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchExceptions)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__BranchExc__Branc__2D47B39A");

            entity.HasOne(d => d.CreatedByStaff).WithMany(p => p.BranchExceptionCreatedByStaffs)
                .HasForeignKey(d => d.CreatedByStaffId)
                .HasConstraintName("FK__BranchExc__Creat__2E3BD7D3");
        });

        modelBuilder.Entity<BranchOperatingHour>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BranchOp__3214EC07A3A269AF");

            entity.HasIndex(e => new { e.BranchId, e.DayOfWeek }, "UQ_BranchId_DayOfWeek").IsUnique();

            entity.Property(e => e.IsDayOff).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchOperatingHours)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__BranchOpe__Branc__30242045");

            entity.HasOne(d => d.UpdatedByStaff).WithMany(p => p.BranchOperatingHours)
                .HasForeignKey(d => d.UpdatedByStaffId)
                .HasConstraintName("FK__BranchOpe__Updat__3118447E");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Roles__3214EC079FD50E25");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.RoleCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RoleName).HasMaxLength(100);
            entity.Property(e => e.Scope)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RoleCreatedByNavigations).HasForeignKey(d => d.CreatedBy);

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.RoleDeletedByNavigations).HasForeignKey(d => d.DeletedBy);

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RoleUpdatedByNavigations).HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Teams");
            entity.ToTable("Teams");

            entity.HasIndex(e => new { e.BranchId, e.Code }, "UQ_Teams_BranchId_Code")
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.SpecialtyTag)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(TeamStatusCode.ACTIVE));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Teams)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Teams_BranchId");

            entity.HasOne(d => d.Leader).WithMany()
                .HasForeignKey(d => d.LeaderId)
                .HasConstraintName("FK_Teams_LeaderId");
        });

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Staffs__3214EC07674AA95B");

            entity.Property(e => e.AccessFailedCount).HasDefaultValue(0);
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CitizenId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.GoogleId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsLockedOut).HasDefaultValue(false);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SecurityStamp)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(StaffStatusCode.ACTIVE));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Staff)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK__Staffs__BranchId__6B0FDBE9");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InverseCreatedByNavigation)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Staffs__CreatedB__6CF8245B");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.InverseDeletedByNavigation)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Staffs__DeletedB__6EE06CCD");

            entity.HasOne(d => d.Role).WithMany(p => p.Staff)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__Staffs__RoleId__6A1BB7B0");

            entity.HasOne(d => d.StaffTier).WithMany(p => p.Staff)
                .HasForeignKey(d => d.StaffTierId)
                .HasConstraintName("FK__Staffs__StaffTie__6C040022");

            entity.HasOne(d => d.Team).WithMany(p => p.Staff)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_Staffs_TeamId");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InverseUpdatedByNavigation)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Staffs__UpdatedB__6DEC4894");
        });

        modelBuilder.Entity<StaffSchedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StaffSch__3214EC0779258F4D");

            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Shift).WithMany(p => p.StaffSchedules)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StaffSche__Shift__70C8B53F");

            entity.HasOne(d => d.Staff).WithMany(p => p.StaffSchedules)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StaffSche__Staff__6FD49106");
        });

        modelBuilder.Entity<StaffTier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StaffTie__3214EC077A692A60");

            entity.Property(e => e.BaseSalary)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.TierName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StaffTierCreatedByNavigations).HasForeignKey(d => d.CreatedBy);

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.StaffTierDeletedByNavigations).HasForeignKey(d => d.DeletedBy);

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.StaffTierUpdatedByNavigations).HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<RevokedAuthToken>(entity =>
        {
            entity.HasKey(e => e.TokenHash);
            entity.Property(e => e.TokenHash).HasMaxLength(64);
            entity.HasIndex(e => e.ExpiresAtUtc);
        });

        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = 3,
                RoleName = "Thủ kho",
                RoleCode = "STORE_KEEPER",
                Scope = "BRANCH",
                Description = "Quản lý vật tư, phụ tùng và điều phối kho.",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = new DateTime(2024, 1, 1),
                UpdatedAt = new DateTime(2024, 1, 1),
                CreatedBy = 1 // Giả định admin đầu tiên
            }
        );
    }
}
