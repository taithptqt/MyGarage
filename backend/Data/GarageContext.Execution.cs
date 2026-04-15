using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    public virtual DbSet<BayAllocation> BayAllocations { get; set; }
    public virtual DbSet<BayStatusLog> BayStatusLogs { get; set; }
    public virtual DbSet<ConsumableReturnBundle> ConsumableReturnBundles { get; set; }
    public virtual DbSet<ConsumableReturnBundleItem> ConsumableReturnBundleItems { get; set; }
    public virtual DbSet<TimesheetEntry> TimesheetEntries { get; set; }
    public virtual DbSet<QcChecklistTemplate> QcChecklistTemplates { get; set; }
    public virtual DbSet<QcChecklistItem> QcChecklistItems { get; set; }
    public virtual DbSet<StockAlert> StockAlerts { get; set; }
    public virtual DbSet<GoodsReceiptNote> GoodsReceiptNotes { get; set; }
    public virtual DbSet<GoodsReceiptNoteItem> GoodsReceiptNoteItems { get; set; }
    public virtual DbSet<GrnDailySnapshot> GrnDailySnapshots { get; set; }

    private void ConfigureExecutionModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BayAllocation>(entity =>
        {
            entity.ToTable("BayAllocations");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BayId, e.AllocatedAt }, "IX_BayAllocations_BayId_AllocatedAt");
            entity.HasIndex(e => new { e.ServiceOrderId, e.ReleasedAt }, "IX_BayAllocations_ServiceOrderId_ReleasedAt");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Bay)
                .WithMany(p => p.BayAllocations)
                .HasForeignKey(d => d.BayId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ServiceOrder)
                .WithMany()
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Vehicle)
                .WithMany()
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.AllocatedByStaff)
                .WithMany()
                .HasForeignKey(d => d.AllocatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReleasedByStaff)
                .WithMany()
                .HasForeignKey(d => d.ReleasedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BayStatusLog>(entity =>
        {
            entity.ToTable("BayStatusLogs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BayId, e.ChangedAt }, "IX_BayStatusLogs_BayId_ChangedAt");
            entity.Property(e => e.OldStatus).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.NewStatus).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Bay)
                .WithMany(p => p.BayStatusLogs)
                .HasForeignKey(d => d.BayId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ChangedByStaff)
                .WithMany()
                .HasForeignKey(d => d.ChangedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ConsumableReturnBundle>(entity =>
        {
            entity.ToTable("ConsumableReturnBundles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BundleCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ServiceOrder)
                .WithMany()
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Technician)
                .WithMany()
                .HasForeignKey(d => d.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReceivedByStaff)
                .WithMany()
                .HasForeignKey(d => d.ReceivedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ConsumableReturnBundleItem>(entity =>
        {
            entity.ToTable("ConsumableReturnBundleItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IssuedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UsedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Unit).HasMaxLength(50);

            entity.HasOne(d => d.Bundle)
                .WithMany(p => p.Items)
                .HasForeignKey(d => d.BundleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.MaterialReqDetail)
                .WithMany()
                .HasForeignKey(d => d.MaterialReqDetailId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TimesheetEntry>(entity =>
        {
            entity.ToTable("TimesheetEntries");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StaffId, e.WorkDate, e.WorkType }, "UQ_TimesheetEntries_Staff_WorkDate_WorkType").IsUnique();
            entity.Property(e => e.CheckInTime).HasColumnName("ClockIn");
            entity.Property(e => e.CheckOutTime).HasColumnName("ClockOut");
            entity.Property(e => e.WorkType).HasColumnName("TaskType").HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.BreakMinutes).HasDefaultValue(0);

            entity.HasOne(d => d.Staff)
                .WithMany()
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Branch)
                .WithMany()
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ApprovedByStaff)
                .WithMany()
                .HasForeignKey(d => d.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QcChecklistTemplate>(entity =>
        {
            entity.ToTable("QcChecklistTemplates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.ServiceCategory).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<QcChecklistItem>(entity =>
        {
            entity.ToTable("QcChecklistItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemName).HasMaxLength(255);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Result).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.FailNote).HasMaxLength(1000);
            entity.Property(e => e.PhotoUrl).HasMaxLength(1000).IsUnicode(false);

            entity.HasOne(d => d.QcInspection)
                .WithMany(p => p.QcChecklistItems)
                .HasForeignKey(d => d.QcInspectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.TemplateItem)
                .WithMany(p => p.InverseTemplateItem)
                .HasForeignKey(d => d.TemplateItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.CheckedByStaff)
                .WithMany()
                .HasForeignKey(d => d.CheckedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockAlert>(entity =>
        {
            entity.ToTable("StockAlerts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProductId, e.WarehouseId, e.IsResolved }, "IX_StockAlerts_Unresolved");
            entity.Property(e => e.AlertType).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.CurrentStock).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.MinStock).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.IsResolved).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Warehouse)
                .WithMany()
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ResolvedByPurchaseOrder)
                .WithMany()
                .HasForeignKey(d => d.ResolvedByPurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GoodsReceiptNote>(entity =>
        {
            entity.ToTable("GoodsReceiptNotes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GrnCode).IsUnique();
            entity.HasIndex(e => new { e.PurchaseOrderId, e.ReceivedAt }, "IX_GoodsReceiptNotes_PurchaseOrderId_ReceivedAt");
            entity.Property(e => e.GrnCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.ReceivedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.PurchaseOrder)
                .WithMany()
                .HasForeignKey(d => d.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Warehouse)
                .WithMany()
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReceivedByStaff)
                .WithMany()
                .HasForeignKey(d => d.ReceivedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GoodsReceiptNoteItem>(entity =>
        {
            entity.ToTable("GoodsReceiptNoteItems");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GoodsReceiptNoteId, "IX_GoodsReceiptNoteItems_GoodsReceiptNoteId");
            entity.HasIndex(e => new { e.PurchaseOrderItemId, e.ProductId }, "IX_GoodsReceiptNoteItems_PurchaseOrderItem_Product");
            entity.Property(e => e.OrderedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReceivedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UnitCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.LineAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.GoodsReceiptNote)
                .WithMany(p => p.Items)
                .HasForeignKey(d => d.GoodsReceiptNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.PurchaseOrderItem)
                .WithMany()
                .HasForeignKey(d => d.PurchaseOrderItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GrnDailySnapshot>(entity =>
        {
            entity.ToTable("GrnDailySnapshots");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BranchId, e.SnapshotDate }, "UX_GrnDailySnapshots_Branch_Date").IsUnique();
            entity.Property(e => e.TotalReceivedAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalReceivedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch)
                .WithMany()
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
