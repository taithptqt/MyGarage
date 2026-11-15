using System;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigureRetailAndVouchers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerVoucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC0716F1AF48");

            entity.Property(e => e.IsUsed).HasDefaultValue(false);

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerVouchers)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CustomerV__Custo__536D5C82");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.CustomerVouchers)
                .HasForeignKey(d => d.RetailOrderId)
                .HasConstraintName("FK__CustomerV__Retai__5649C92D");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.CustomerVouchers)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK__CustomerV__Servi__5555A4F4");

            entity.HasOne(d => d.Voucher).WithMany(p => p.CustomerVouchers)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CustomerV__Vouch__546180BB");
        });

        modelBuilder.Entity<RetailOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RetailOr__3214EC07E6EE6099");

            entity.HasIndex(e => e.OrderCode, "UQ__RetailOr__999B5229199338C4").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CustomerNotes).HasMaxLength(500);
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OrderCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(RetailOrderStatus.DRAFT));
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TierDiscountAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalDiscountAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.VoucherDiscountAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.RetailOrders)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailOrd__Branc__2E06CDA9");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RetailOrderCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__RetailOrd__Creat__2FEF161B");

            entity.HasOne(d => d.Customer).WithMany(p => p.RetailOrders)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__RetailOrd__Custo__2EFAF1E2");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RetailOrderUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__RetailOrd__Updat__30E33A54");

            entity.HasOne(d => d.Voucher).WithMany(p => p.RetailOrders)
                .HasForeignKey(d => d.VoucherId)
                .HasConstraintName("FK_RetailOrders_VoucherId");
        });

        modelBuilder.Entity<RetailOrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RetailOr__3214EC0757E0441D");

            entity.Property(e => e.CostPriceAtTime).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxRate)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.UnitPriceAtTime).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.RetailOrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailOrd__Produ__2D12A970");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.RetailOrderItems)
                .HasForeignKey(d => d.RetailOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailOrd__Retai__2C1E8537");
        });

        modelBuilder.Entity<RetailReturn>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RetailRe__3214EC07D241738E");

            entity.HasIndex(e => e.ReturnCode, "UQ__RetailRe__4CF726C98A4E23DB").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.ReturnCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ReturnDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(RetailReturnStatusCode.PENDING));
            entity.Property(e => e.TotalRefundAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.RetailReturnApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__RetailRet__Appro__39788055");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RetailReturnCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Creat__3A6CA48E");

            entity.HasOne(d => d.ReceivedByNavigation).WithMany(p => p.RetailReturnReceivedByNavigations)
                .HasForeignKey(d => d.ReceivedBy)
                .HasConstraintName("FK__RetailRet__Recei__38845C1C");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.RetailReturns)
                .HasForeignKey(d => d.RetailOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Retai__379037E3");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RetailReturnUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__RetailRet__Updat__3B60C8C7");
        });

        modelBuilder.Entity<RetailReturnItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RetailRe__3214EC0700BDCE4A");

            entity.HasIndex(e => new { e.RetailReturnId, e.RetailOrderItemId }, "UQ_RetailReturnItem_Ticket").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ItemCondition)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.RefundAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ReturnedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(RetailReturnItemStatusCode.PENDING));

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RetailReturnItemCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Creat__35A7EF71");

            entity.HasOne(d => d.RetailOrderItem).WithMany(p => p.RetailReturnItems)
                .HasForeignKey(d => d.RetailOrderItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Retai__33BFA6FF");

            entity.HasOne(d => d.RetailReturn).WithMany(p => p.RetailReturnItems)
                .HasForeignKey(d => d.RetailReturnId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Retai__32CB82C6");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RetailReturnItemUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__RetailRet__Updat__369C13AA");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.RetailReturnItems)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RetailRet__Wareh__34B3CB38");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Vouchers__3214EC070B87BA0F");

            entity.HasIndex(e => e.Code, "UQ__Vouchers__A25C5AA7E33C4EC6").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsStackable).HasDefaultValue(false);
            entity.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinOrderValue)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UsageLimit).HasDefaultValue(0);
            entity.Property(e => e.UsedCount).HasDefaultValue(0);
            entity.Property(e => e.Value).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VoucherCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Vouchers__Create__10416098");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.VoucherDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Vouchers__Delete__1229A90A");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VoucherUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Vouchers__Update__113584D1");
        });
    }
}
