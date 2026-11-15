using System;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigureAppointmentsAndBays(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Appointm__3214EC0762F6D2AE");

            entity.HasIndex(e => e.GroupSessionId, "IX_Appointments_GroupSessionId");

            entity.Property(e => e.GroupSessionId).HasColumnName("GroupSessionId");
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.InsuranceClaimMode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.InsuranceDeductibleAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceDeductibleOption)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.InsuranceQuoteState)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.IsReminderSent).HasDefaultValue(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(AppointmentStatus.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Appointme__Branc__1B29035F");

            entity.HasOne(d => d.GroupSession).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.GroupSessionId)
                .HasConstraintName("FK_Appointments_GroupSessionId");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Appointme__Creat__1E05700A");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AppointmentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy);

            entity.HasOne(d => d.Customer).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Appointme__Custo__1C1D2798");

            entity.HasOne(d => d.InsuranceCompany).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.InsuranceCompanyId)
                .HasConstraintName("FK_Appointments_InsuranceCompanyId");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__Appointme__Vehic__1D114BD1");
        });

        modelBuilder.Entity<AppointmentInsuranceAsset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_AppointmentInsuranceAssets");

            entity.HasIndex(e => new { e.AppointmentId, e.AssetType, e.SortOrder }, "IX_AppointmentInsuranceAssets_Appointment_Type_Order");

            entity.Property(e => e.AssetType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Appointment).WithMany(p => p.InsuranceAssets)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AppointmentInsuranceAssets_Appointments");
        });

        modelBuilder.Entity<AppointmentInsuranceUpload>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_AppointmentInsuranceUploads");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_AppointmentInsuranceUploads_Status_CreatedAt");

            entity.Property(e => e.AssetType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.BlobPath)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Appointment).WithMany(p => p.InsuranceUploads)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_AppointmentInsuranceUploads_Appointments");
        });

        modelBuilder.Entity<GroupSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BookingG__3214EC077B6FE4D1");
            entity.ToTable("GroupSessions", tb =>
            {
                tb.HasCheckConstraint(
                    "CK_BookingGroups_Status",
                    $"[Status] IN ('{nameof(BookingGroupStatus.PENDING)}','{nameof(BookingGroupStatus.CONFIRMED)}','{nameof(BookingGroupStatus.PARTIAL_CONFIRMED)}','{nameof(BookingGroupStatus.CANCELED)}','{nameof(BookingGroupStatus.COMPLETED)}')");
                tb.HasCheckConstraint("CK_GroupSessions_ItemCounters", "[TotalItems] >= 0 AND [ConfirmedItems] >= 0 AND [CanceledItems] >= 0 AND [ConfirmedItems] + [CanceledItems] <= [TotalItems]");
            });

            entity.HasIndex(e => e.GroupCode, "UQ_GroupSessions_GroupCode").IsUnique();

            entity.HasIndex(e => new { e.CustomerId, e.CreatedAt }, "IX_GroupSessions_CustomerId_CreatedAt");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_GroupSessions_Status_CreatedAt");

            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.ConfirmedAppointments)
                .HasDefaultValue(0)
                .HasColumnName("ConfirmedItems");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.GroupCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue(nameof(BookingGroupStatus.PENDING));
            entity.Property(e => e.TotalAppointments)
                .HasDefaultValue(0)
                .HasColumnName("TotalItems");
            entity.Property(e => e.CanceledAppointments)
                .HasDefaultValue(0)
                .HasColumnName("CanceledItems");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.ToTable("GroupSessions", tb =>
            {
                tb.HasCheckConstraint("CK_GroupSessions_Status", "[Status] IN ('PENDING','CONFIRMED','PARTIAL_CONFIRMED','CANCELED','COMPLETED')");
                tb.HasCheckConstraint("CK_GroupSessions_ItemCounters", "[TotalItems] >= 0 AND [ConfirmedItems] >= 0 AND [CanceledItems] >= 0 AND [ConfirmedItems] + [CanceledItems] <= [TotalItems]");
            });

            entity.HasOne(d => d.Branch).WithMany(p => p.GroupSessions)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GroupSessions_BranchId");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.GroupSessionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_GroupSessions_CreatedBy");

            entity.HasOne(d => d.Customer).WithMany(p => p.GroupSessions)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GroupSessions_CustomerId");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.GroupSessionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_GroupSessions_UpdatedBy");
        });

        modelBuilder.Entity<Bay>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Bays__3214EC07708F9CE3");

            entity.Property(e => e.BayType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ServiceArea)
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsRequired();
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.MaxTonnage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.SequenceOrder).HasDefaultValue(0);
            entity.Property(e => e.OperationalStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(BayOperationalStatusCode.ACTIVE));
            entity.Property(e => e.ReservationStatus).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Bays)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bays__BranchId__24B26D99");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.BayCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Bays__CreatedBy__269AB60B");

            entity.HasOne(d => d.CurrentOccupiedVehicle).WithMany(p => p.Bays)
                .HasForeignKey(d => d.CurrentOccupiedVehicleId)
                .HasConstraintName("FK__Bays__CurrentOcc__25A691D2");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.BayDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Bays__DeletedBy__2882FE7D");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.BayUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Bays__UpdatedBy__278EDA44");
        });

        modelBuilder.Entity<CapacityConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_CapacityConfigs");

            entity.Property(e => e.BookingThresholdPercent).HasColumnType("decimal(5, 4)");
            entity.Property(e => e.CreateAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdateAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany()
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_CapacityConfigs_Branch");

            entity.HasOne(d => d.CreateByNavigation).WithMany()
                .HasForeignKey(d => d.CreateBy)
                .HasConstraintName("FK_CapacityConfigs_CreateBy");

            entity.HasOne(d => d.UpdateByNavigation).WithMany()
                .HasForeignKey(d => d.UpdateBy)
                .HasConstraintName("FK_CapacityConfigs_UpdateBy");
        });

        modelBuilder.Entity<WorkSlot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__WorkSlot__3214EC0774196CAC");

            entity.Property(e => e.IsAdditional).HasDefaultValue(false);
            entity.Property(e => e.SequenceOrder).HasDefaultValue(1);
            entity.Property(e => e.SlotType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(WorkSlotStatus.RESERVED));

            entity.HasOne(d => d.Appointment).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_WorkSlots_AppointmentId");

            entity.HasOne(d => d.Bay).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.BayId)
                .HasConstraintName("FK__WorkSlots__BayId__19CACAD2");

            entity.HasOne(d => d.Branch).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__WorkSlots__Branc__18D6A699");

            entity.HasOne(d => d.Customer).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__WorkSlots__Custo__1BB31344");

            entity.HasOne(d => d.ParentSlot).WithMany(p => p.InverseParentSlot)
                .HasForeignKey(d => d.ParentSlotId)
                .HasConstraintName("FK__WorkSlots__Paren__17E28260");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_WorkSlots_ServiceOrderId");

            entity.HasOne(d => d.Technician).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.TechnicianId)
                .HasConstraintName("FK__WorkSlots__Techn__1ABEEF0B");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.WorkSlots)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__WorkSlots__Vehic__1CA7377D");
        });
    }
}
