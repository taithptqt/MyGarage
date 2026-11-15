using System;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigureInventoryAndPurchasing(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Categori__3214EC0799C9DC1C");

            entity.Property(e => e.CategoryName).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsDepositRequired).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CategoryCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Categorie__Creat__33008CF0");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CategoryDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Categorie__Delet__34E8D562");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("FK__Categorie__Paren__320C68B7");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CategoryUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Categorie__Updat__33F4B129");
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Inventor__3214EC07EA32CA02");

            entity.HasIndex(e => new { e.WarehouseId, e.ProductId }, "UQ_Warehouse_Product").IsUnique();

            entity.Property(e => e.CurrentMacPrice)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("Current_MAC_Price");
            entity.Property(e => e.LocationBin)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MaxStockLevel).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.MinStockLevel)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReservedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.TotalInventoryValue)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.Inventories)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventori__Produ__6F1576F7");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.Inventories)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventori__Wareh__6E2152BE");
        });

        modelBuilder.Entity<InventoryAudit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Inventor__3214EC07DF0AA8BB");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(InventoryAuditStatusCode.DRAFT));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.InventoryAuditApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__Inventory__Appro__72E607DB");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InventoryAuditCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Creat__73DA2C14");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InventoryAuditUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Inventory__Updat__74CE504D");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.InventoryAudits)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Wareh__71F1E3A2");
        });

        modelBuilder.Entity<InventoryAuditDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Inventor__3214EC07DA0B0DAD");

            entity.Property(e => e.ActualQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.DifferenceQty)
                .HasComputedColumnSql("([ActualQty]-[SystemQty])", true)
                .HasColumnType("decimal(19, 3)");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.SystemQty).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Audit).WithMany(p => p.InventoryAuditDetails)
                .HasForeignKey(d => d.AuditId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Audit__70099B30");

            entity.HasOne(d => d.Product).WithMany(p => p.InventoryAuditDetails)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Produ__70FDBF69");
        });

        modelBuilder.Entity<MaterialRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Material__3214EC07FEAF631C");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsRework).HasDefaultValue(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.RequestDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(MaterialRequestStatusCode.PENDING));

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_MaterialRequests_ServiceOrderId");

            entity.HasOne(d => d.Storekeeper).WithMany(p => p.MaterialRequestStorekeepers)
                .HasForeignKey(d => d.StorekeeperId)
                .HasConstraintName("FK__MaterialR__Store__041093DD");

            entity.HasOne(d => d.Technician).WithMany(p => p.MaterialRequestTechnicians)
                .HasForeignKey(d => d.TechnicianId)
                .HasConstraintName("FK__MaterialR__Techn__031C6FA4");
        });

        modelBuilder.Entity<MaterialRequestDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Material__3214EC07D90DB7BF");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IssuedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.RequestedCondition)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RequestedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReturnedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(MaterialRequestDetailStatusCode.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.WastedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MaterialRequestDetailCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__MaterialR__Creat__004002F9");

            entity.HasOne(d => d.Product).WithMany(p => p.MaterialRequestDetails)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialR__Produ__7E57BA87");

            entity.HasOne(d => d.ServiceOrderDetail).WithMany(p => p.MaterialRequestDetails)
                .HasForeignKey(d => d.ServiceOrderDetailId)
                .HasConstraintName("FK_MaterialRequestDetails_ServiceOrderDetailId");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MaterialRequestDetailUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__MaterialR__Updat__01342732");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.MaterialRequestDetails)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialR__Wareh__7F4BDEC0");
        });

        modelBuilder.Entity<MaterialReturn>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Material__3214EC07BC10D807");

            entity.HasIndex(e => e.ReturnCode, "UQ__Material__4CF726C9D2977954").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ItemCondition)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(255);
            entity.Property(e => e.ReceivedByStaffId).HasColumnName("ReceivedBy_StaffId");
            entity.Property(e => e.ReturnCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ReturnUnit).HasMaxLength(50);
            entity.Property(e => e.ReturnedByStaffId).HasColumnName("ReturnedBy_StaffId");
            entity.Property(e => e.ReturnedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(MaterialReturnStatusCode.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MaterialReturnCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__MaterialR__Creat__09C96D33");

            entity.HasOne(d => d.MaterialRequestDetail).WithMany(p => p.MaterialReturns)
                .HasForeignKey(d => d.MaterialRequestDetailId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialR__Mater__05F8DC4F");

            entity.HasOne(d => d.Product).WithMany(p => p.MaterialReturns)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialR__Produ__06ED0088");

            entity.HasOne(d => d.ReceivedByStaff).WithMany(p => p.MaterialReturnReceivedByStaffs)
                .HasForeignKey(d => d.ReceivedByStaffId)
                .HasConstraintName("FK__MaterialR__Recei__08D548FA");

            entity.HasOne(d => d.ReturnedByStaff).WithMany(p => p.MaterialReturnReturnedByStaffs)
                .HasForeignKey(d => d.ReturnedByStaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MaterialR__Retur__07E124C1");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.MaterialReturns)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_MaterialReturns_ServiceOrderId");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MaterialReturnUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__MaterialR__Updat__0ABD916C");
        });

        modelBuilder.Entity<PartInventoryReservation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_PartInventoryReservations");

            entity.Property(e => e.CreateAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ReservedQuantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("RESERVED");
            entity.Property(e => e.UpdateAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreateByNavigation).WithMany()
                .HasForeignKey(d => d.CreateBy)
                .HasConstraintName("FK_PartInventoryReservations_CreateBy");

            entity.HasOne(d => d.Product).WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartInventoryReservations_Product");

            entity.HasOne(d => d.UpdateByNavigation).WithMany()
                .HasForeignKey(d => d.UpdateBy)
                .HasConstraintName("FK_PartInventoryReservations_UpdateBy");

            entity.HasOne(d => d.WorkSlot).WithMany()
                .HasForeignKey(d => d.WorkSlotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartInventoryReservations_WorkSlot");
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Packages");

            entity.HasIndex(e => e.Code, "UQ_Packages_Code").IsUnique();

            entity.Property(e => e.BasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Category).WithMany()
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Packages_ServiceCategories");
        });

        modelBuilder.Entity<PackageItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_PackageItems");

            entity.HasIndex(e => new { e.PackageId, e.StandardServiceId }, "UQ_PackageItems_Package_Service").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDefault).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Package).WithMany(p => p.PackageItems)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PackageItems_Packages");

            entity.HasOne(d => d.StandardService).WithMany()
                .HasForeignKey(d => d.StandardServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PackageItems_StandardServices");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Products__3214EC07968B91BC");

            entity.HasIndex(e => e.Sku, "UQ__Products__CA1ECF0DDDECDF2B").IsUnique();

            entity.Property(e => e.CostPrice)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsStrictCapacityControl).HasDefaultValue(false);
            entity.Property(e => e.MinStockLevel).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.ReorderPoint).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RequireEmptyReturn).HasDefaultValue(false);
            entity.Property(e => e.ConsumableThresholdAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsConsumable).HasDefaultValue(false);
            entity.Property(e => e.RetailPrice)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ServicePrice)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Sku)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SKU");
            entity.Property(e => e.TaxRate)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.WarrantyMonths).HasDefaultValue(0);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__Products__Catego__1DD065E0");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProductCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Products__Create__1EC48A19");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.ProductDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Products__Delete__20ACD28B");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProductUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Products__Update__1FB8AE52");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductI__3214EC0756CDEDF5");

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.ImageUrl)
                .HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.IsMain).HasDefaultValue(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ProductIm__Produ__1CDC41A7");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Purchase__3214EC07E8647D31");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(PurchaseOrderStatusCode.PENDING));
            entity.Property(e => e.TotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.ApprovedByStaff).WithMany(p => p.PurchaseOrderApprovedByStaffs)
                .HasForeignKey(d => d.ApprovedByStaffId)
                .HasConstraintName("FK__PurchaseO__Appro__2665ABE1");

            entity.HasOne(d => d.Branch).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Branc__23893F36");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PurchaseOrderCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__PurchaseO__Creat__2759D01A");

            entity.HasOne(d => d.DestinationWarehouse).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.DestinationWarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Desti__247D636F");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Suppl__257187A8");
        });

        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Purchase__3214EC071913A1D3");

            entity.Property(e => e.CostPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OrderedQuantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReceivedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Product).WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Produ__22951AFD");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(d => d.PurchaseOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Purch__21A0F6C4");
        });

        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockTra__3214EC07104DE41B");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Type)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__StockTran__Creat__795DFB40");

            entity.HasOne(d => d.MaterialReturn).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.MaterialReturnId)
                .HasConstraintName("FK_StockTransactions_MaterialReturnId");

            entity.HasOne(d => d.Product).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StockTran__Produ__7869D707");

            entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.PurchaseOrderId)
                .HasConstraintName("FK_StockTransactions_PurchaseOrderId");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.RetailOrderId)
                .HasConstraintName("FK_StockTransactions_RetailOrderId");

            entity.HasOne(d => d.RetailReturn).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.RetailReturnId)
                .HasConstraintName("FK_StockTransactions_RetailReturnId");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Supplier__3214EC077AD2834E");

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.ContactName).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CurrentDebt)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SupplierName).HasMaxLength(255);
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SupplierCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Suppliers__Creat__7E22B05D");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.SupplierDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Suppliers__Delet__000AF8CF");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SupplierUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Suppliers__Updat__7F16D496");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Warehous__3214EC07FE923BC8");

            entity.HasIndex(e => e.Code, "UQ__Warehous__A25C5AA73F13B012").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Warehouses)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Warehouse__Branc__131DCD43");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.WarehouseCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Warehouse__Creat__150615B5");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.WarehouseDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Warehouse__Delet__16EE5E27");

            entity.HasOne(d => d.Manager).WithMany(p => p.WarehouseManagers)
                .HasForeignKey(d => d.ManagerId)
                .HasConstraintName("FK__Warehouse__Manag__1411F17C");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.WarehouseUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Warehouse__Updat__15FA39EE");
        });
    }
}
