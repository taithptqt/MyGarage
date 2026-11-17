using System;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigurePayrollAndAccounting(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommissionRule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Commissi__3214EC07042111C8");

            entity.Property(e => e.CommissionType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CommissionValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.TargetType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.CommissionRules)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Commissio__Branc__3AA1AEB8");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CommissionRuleCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Commissio__Creat__37C5420D");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CommissionRuleDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Commissio__Delet__39AD8A7F");

            entity.HasOne(d => d.StaffTier).WithMany(p => p.CommissionRules)
                .HasForeignKey(d => d.StaffTierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Commissio__Staff__36D11DD4");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CommissionRuleUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Commissio__Updat__38B96646");
        });

        modelBuilder.Entity<CommissionTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Commissi__3214EC07C2FCF5BE");

            entity.Property(e => e.AppliedCommissionType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.AppliedCommissionValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AppliedSplitRatio)
                .HasDefaultValue(100m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.BaseAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CommissionAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.LogType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SourceType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(CommissionTransactionStatusCode.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.CommissionTransactionApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__Commissio__Appro__3D7E1B63");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CommissionTransactionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Commissio__Creat__3E723F9C");

            entity.HasOne(d => d.OriginalCommission).WithMany(p => p.InverseOriginalCommission)
                .HasForeignKey(d => d.OriginalCommissionId)
                .HasConstraintName("FK__Commissio__Origi__3C89F72A");

            entity.HasOne(d => d.RetailReturn).WithMany(p => p.CommissionTransactions)
                .HasForeignKey(d => d.RetailReturnId)
                .HasConstraintName("FK_CommissionTransactions_RetailReturnId");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.CommissionTransactions)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_CommissionTransactions_ServiceOrderId");

            entity.HasOne(d => d.ServiceStaffAssignment).WithMany(p => p.CommissionTransactions)
                .HasForeignKey(d => d.ServiceStaffAssignmentId)
                .HasConstraintName("FK_CommissionTransactions_ServiceStaffAssignmentId");

            entity.HasOne(d => d.Staff).WithMany(p => p.CommissionTransactionStaffs)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Commissio__Staff__3B95D2F1");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CommissionTransactionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Commissio__Updat__3F6663D5");
        });

        modelBuilder.Entity<ExpenseLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ExpenseL__3214EC07B63E3350");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ExpenseType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProofUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ExpenseLogStatusCode.PENDING));

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.ExpenseLogApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__ExpenseLo__Appro__668030F6");

            entity.HasOne(d => d.Branch).WithMany(p => p.ExpenseLogs)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExpenseLo__Branc__63A3C44B");

            entity.HasOne(d => d.ShiftClosing).WithMany(p => p.ExpenseLogs)
                .HasForeignKey(d => d.ShiftClosingId)
                .HasConstraintName("FK__ExpenseLo__Shift__6497E884");

            entity.HasOne(d => d.Staff).WithMany(p => p.ExpenseLogStaffs)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("FK__ExpenseLo__Staff__658C0CBD");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Invoices__3214EC077603BEDB");

            entity.Property(e => e.DueDate).HasColumnType("date");

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CompanyAddress).HasMaxLength(500);
            entity.Property(e => e.CompanyName).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.InvoiceUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.PaidAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PayerType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(InvoicePaymentStatusCode.UNPAID));
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ReceiveEmail)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SubTotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InvoiceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Invoices__Create__7C6F7215");

            entity.HasOne(d => d.CreditApprovedByStaff).WithMany(p => p.InvoiceCreditApprovedByStaffs)
                .HasForeignKey(d => d.CreditApprovedByStaffId)
                .HasConstraintName("FK__Invoices__Credit__7B7B4DDC");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.RetailOrderId)
                .HasConstraintName("FK__Invoices__Retail__7993056A");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK__Invoices__Servic__789EE131");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InvoiceUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Invoices__Update__7D63964E");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK__Invoices__Vehicl__7A8729A3");
        });

        modelBuilder.Entity<InvoiceDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__InvoiceD__3214EC07BA422D95");

            entity.Property(e => e.CostPriceAtTime).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountAllocatedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ItemName).HasMaxLength(255);
            entity.Property(e => e.ItemType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxRate)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UnitPriceAtTime).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceDetails)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__InvoiceDe__Invoi__75C27486");

            entity.HasOne(d => d.Product).WithMany(p => p.InvoiceDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__InvoiceDe__Produ__76B698BF");

            entity.HasOne(d => d.StandardService).WithMany(p => p.InvoiceDetails)
                .HasForeignKey(d => d.StandardServiceId)
                .HasConstraintName("FK__InvoiceDe__Stand__77AABCF8");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Payments__3214EC07735396A5");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.PayerType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(PaymentStatusCode.COMPLETED));
            entity.Property(e => e.TransactionCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Direction)
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired()
                .HasDefaultValue("INFLOW");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Cashier).WithMany(p => p.PaymentCashiers)
                .HasForeignKey(d => d.CashierId)
                .HasConstraintName("FK__Payments__Cashie__162F4418");

            entity.HasOne(d => d.Invoice).WithMany(p => p.Payments)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("FK__Payments__Invoic__18178C8A");

            entity.HasOne(d => d.Method).WithMany(p => p.Payments)
                .HasForeignKey(d => d.MethodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payments__Method__19FFD4FC");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.Payments)
                .HasForeignKey(d => d.RetailOrderId)
                .HasConstraintName("FK__Payments__Retail__190BB0C3");

            entity.HasOne(d => d.ReversedPayment).WithMany(p => p.InverseReversedPayment)
                .HasForeignKey(d => d.ReversedPaymentId)
                .HasConstraintName("FK__Payments__Revers__1AF3F935");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.Payments)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK__Payments__Servic__17236851");

            entity.HasOne(d => d.ShiftClosing).WithMany(p => p.Payments)
                .HasForeignKey(d => d.ShiftClosingId)
                .HasConstraintName("FK__Payments__ShiftC__153B1FDF");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PaymentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Payments__Update__1BE81D6E");
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PaymentM__3214EC07B07F4781");

            entity.HasIndex(e => e.MethodCode, "UQ__PaymentM__11E9210D55BD757B").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.MethodCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MethodName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PaymentMethodCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__PaymentMe__Creat__125EB334");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.PaymentMethodDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__PaymentMe__Delet__1446FBA6");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PaymentMethodUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__PaymentMe__Updat__1352D76D");
        });

        modelBuilder.Entity<RevenueSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RevenueS__3214EC07FCDD558C");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NetCashFlow)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetProfit)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalCollected)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalDebt)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalExpense)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalRevenue)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.RevenueSnapshots)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RevenueSn__Branc__3C54ED00");
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Shifts__3214EC0704926738");

            entity.Property(e => e.ShiftName).HasMaxLength(100);

            entity.HasOne(d => d.Branch).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Shifts__BranchId__69279377");
        });

        modelBuilder.Entity<ShiftClosing>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ShiftClo__3214EC07117EA13A");

            entity.Property(e => e.ActualBalance)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Discrepancy)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ExpectedBalance)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.OpeningBalance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ShiftClosingStatusCode.OPEN));
            entity.Property(e => e.TotalCashIn)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalCashOut)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.ShiftClosings)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ShiftClos__Branc__65570293");

            entity.HasOne(d => d.Cashier).WithMany(p => p.ShiftClosingCashiers)
                .HasForeignKey(d => d.CashierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ShiftClos__Cashi__673F4B05");

            entity.HasOne(d => d.Manager).WithMany(p => p.ShiftClosingManagers)
                .HasForeignKey(d => d.ManagerId)
                .HasConstraintName("FK__ShiftClos__Manag__68336F3E");

            entity.HasOne(d => d.WorkShift).WithMany(p => p.ShiftClosings)
                .HasForeignKey(d => d.WorkShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ShiftClos__WorkS__664B26CC");
        });
    }
}
