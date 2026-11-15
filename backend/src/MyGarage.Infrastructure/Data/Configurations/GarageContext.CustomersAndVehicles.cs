using System;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigureCustomersAndVehicles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC071C60D783");

            entity.HasIndex(e => e.Phone, "IX_Customers_Phone");

            entity.Property(e => e.AccessFailedCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.GoogleId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsLockedOut).HasDefaultValue(false);
            entity.Property(e => e.NoShowCount).HasDefaultValue(0);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SecurityStamp)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("GUEST");
            entity.Property(e => e.TotalSpending)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CustomerCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Customers__Creat__48EFCE0F");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CustomerDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Customers__Delet__4AD81681");

            entity.HasOne(d => d.Tier).WithMany(p => p.Customers)
                .HasForeignKey(d => d.TierId)
                .HasConstraintName("FK__Customers__TierI__47FBA9D6");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CustomerUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Customers__Updat__49E3F248");
        });

        modelBuilder.Entity<CustomerFeedback>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC07F1754D23");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerFeedbacks)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CustomerF__Custo__451F3D2B");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.CustomerFeedbacks)
                .HasForeignKey(d => d.RetailOrderId)
                .HasConstraintName("FK_CustomerFeedbacks_RetailOrderId");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.CustomerFeedbacks)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_CustomerFeedbacks_ServiceOrderId");
        });

        modelBuilder.Entity<CustomerTier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC07163563D7");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DiscountPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.MinSpending).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TierName).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CustomerTierCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__CustomerT__Creat__4BCC3ABA");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CustomerTierDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__CustomerT__Delet__4DB4832C");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CustomerTierUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__CustomerT__Updat__4CC05EF3");
        });

        modelBuilder.Entity<CustomerVehicle>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC07D8CEE2BC");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsCurrentOwner).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CustomerVehicleCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__CustomerV__Creat__5090EFD7");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerVehicles)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CustomerV__Custo__4EA8A765");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CustomerVehicleDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__CustomerV__Delet__52793849");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CustomerVehicleUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__CustomerV__Updat__51851410");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.CustomerVehicles)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CustomerV__Vehic__4F9CCB9E");
        });

        modelBuilder.Entity<InsuranceCompany>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Insuranc__3214EC07749DA508");

            entity.Property(e => e.ContactPerson).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CreditLimit)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CurrentDebt)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InsuranceCompanyCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Insurance__Creat__6B44E613");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.InsuranceCompanyDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Insurance__Delet__6D2D2E85");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InsuranceCompanyUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Insurance__Updat__6C390A4C");
        });

        modelBuilder.Entity<ModelSpecificRule>(entity =>
        {
            entity.HasKey(e => new { e.ModelId, e.ProductId }).HasName("PK__ModelSpe__23976D404AEA9849");

            entity.Property(e => e.StandardCapacity).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Tolerance)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Model).WithMany(p => p.ModelSpecificRules)
                .HasForeignKey(d => d.ModelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ModelSpec__Model__0CA5D9DE");

            entity.HasOne(d => d.Product).WithMany(p => p.ModelSpecificRules)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ModelSpec__Produ__0D99FE17");
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Vehicles__3214EC0730F8DFC4");

            entity.HasIndex(e => e.LicensePlate, "UQ_Vehicles_LicensePlate")
                .IsUnique()
                .HasFilter("([LicensePlate] IS NOT NULL)");

            entity.HasIndex(e => e.Vin, "UQ_Vehicles_VIN")
                .IsUnique()
                .HasFilter("([VIN] IS NOT NULL)");

            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.LicensePlate)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Odo)
                .HasDefaultValue(0)
                .HasColumnName("ODO");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Vin)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("VIN");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VehicleCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Vehicles__Create__0D64F3ED");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.VehicleDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__Vehicles__Delete__0F4D3C5F");

            entity.HasOne(d => d.Model).WithMany(p => p.Vehicles)
                .HasForeignKey(d => d.ModelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Vehicles__ModelI__0C70CFB4");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VehicleUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Vehicles__Update__0E591826");
        });

        modelBuilder.Entity<VehicleBrand>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__VehicleB__3214EC07EADA13FE");

            entity.HasIndex(e => e.Code, "UQ__VehicleB__A25C5AA7E9C6A8B3").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CountryOfOrigin).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.LogoUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VehicleBrandCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__VehicleBr__Creat__00FF1D08");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.VehicleBrandDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__VehicleBr__Delet__02E7657A");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VehicleBrandUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__VehicleBr__Updat__01F34141");
        });

        modelBuilder.Entity<VehicleConditionLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__VehicleC__3214EC075745CAC4");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CustomerSignatureUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.DashboardWarningLights).HasMaxLength(500);
            entity.Property(e => e.LogType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Valuables).HasMaxLength(1000);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VehicleConditionLogCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__VehicleCo__Creat__05C3D225");

            entity.HasOne(d => d.Inspector).WithMany(p => p.VehicleConditionLogInspectors)
                .HasForeignKey(d => d.InspectorId)
                .HasConstraintName("FK__VehicleCo__Inspe__04CFADEC");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.VehicleConditionLogs)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__VehicleCo__Servi__03DB89B3");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VehicleConditionLogUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<VehicleInsurance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__VehicleI__3214EC07269DD274");

            entity.Property(e => e.PolicyNumber)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.InsuranceCompany).WithMany(p => p.VehicleInsurances)
                .HasForeignKey(d => d.InsuranceCompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__VehicleIn__Insur__07AC1A97");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.VehicleInsurances)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__VehicleIn__Vehic__06B7F65E");
        });

        modelBuilder.Entity<VehicleModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__VehicleM__3214EC0789BEED56");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CylinderCapacity).HasMaxLength(50);
            entity.Property(e => e.FuelType).HasMaxLength(50);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.ModelName).HasMaxLength(100);
            entity.Property(e => e.Segment).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Brand).WithMany(p => p.VehicleModels)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__VehicleMo__Brand__08A03ED0");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VehicleModelCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__VehicleMo__Creat__09946309");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.VehicleModelDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__VehicleMo__Delet__0B7CAB7B");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VehicleModelUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__VehicleMo__Updat__0A888742");
        });
    }
}
