using System;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigureWorkshop(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Deliveri__3214EC070B1DD92B");

            entity.Property(e => e.CarrierName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ShippingAddress).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(DeliveryStatusCode.PENDING));
            entity.Property(e => e.TrackingCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DeliveryCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Deliverie__Creat__5832119F");

            entity.HasOne(d => d.RetailOrder).WithMany(p => p.Deliveries)
                .HasForeignKey(d => d.RetailOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Deliverie__Retai__573DED66");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DeliveryUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Deliverie__Updat__592635D8");
        });

        modelBuilder.Entity<GatePass>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__GatePass__3214EC07516211D2");

            entity.ToTable("GatePass");

            entity.HasIndex(e => e.QrCode, "UQ__GatePass__EE32FA44C549FC8E").IsUnique();

            entity.Property(e => e.ClearanceType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsOldPartsChecked).HasDefaultValue(false);
            entity.Property(e => e.OldPartsPackingListUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.QrCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(GatePassStatusCode.ISSUED));

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.GatePassApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__GatePass__Approv__695C9DA1");

            entity.HasOne(d => d.IssuedByNavigation).WithMany(p => p.GatePassIssuedByNavigations)
                .HasForeignKey(d => d.IssuedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GatePass__Issued__68687968");

            entity.HasOne(d => d.ScannedByNavigation).WithMany(p => p.GatePassScannedByNavigations)
                .HasForeignKey(d => d.ScannedBy)
                .HasConstraintName("FK__GatePass__Scanne__6A50C1DA");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.GatePasses)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GatePass__Servic__6774552F");
        });

        modelBuilder.Entity<ServiceOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceO__3214EC07A6BCD573");

            entity.HasIndex(e => e.GroupSessionId, "IX_ServiceOrders_GroupSessionId");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_ServiceOrders_Status_CreatedAt");

            entity.HasIndex(e => e.Code, "UQ__ServiceO__A25C5AA7FBF72344").IsUnique();

            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.HandoverReceiverName).HasMaxLength(255);
            entity.Property(e => e.HandoverReceiverPhone).HasMaxLength(30);
            entity.Property(e => e.HandoverNotes).HasMaxLength(2000);
            entity.Property(e => e.ArrivalTime).HasColumnType("datetime2");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DepositForfeitedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DepositRefundedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DepositStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderDepositStatusCode.NOT_REQUIRED));
            entity.Property(e => e.FinalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
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
            entity.Property(e => e.OldPartsReturnOption)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OrderCategory)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderPaymentStatusCode.UNPAID));
            entity.Property(e => e.QcinspectorId).HasColumnName("QCInspectorId");
            entity.Property(e => e.Qcstatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderQcStatusCode.PENDING))
                .HasColumnName("QCStatus");
            entity.Property(e => e.QcverifiedAt).HasColumnName("QCVerifiedAt");
            entity.Property(e => e.RequiredDepositAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RequestedPackageIds).HasMaxLength(500);
            entity.Property(e => e.RevisionNumber).HasDefaultValue(1);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderLifecycleStatus.NEW));
            entity.Property(e => e.SubTotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalDepositedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
#pragma warning disable CS0618
            entity.Ignore(e => e.TotalDiscount);
#pragma warning restore CS0618
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.ToTable("ServiceOrders", tb =>
            {
                tb.HasCheckConstraint("CK_ServiceOrders_Status", "[Status] IN ('NEW','CHECKED_IN','WAITING','DIAGNOSING','QUOTING','WAITING_APPROVAL','APPROVED','IN_PROGRESS','REPAIRING','READY','WAITING_PAYMENT','CLOSED','CANCELED')");
                tb.HasCheckConstraint("CK_ServiceOrders_OrderCategory", "[OrderCategory] IS NULL OR ([OrderCategory]='WARRANTLY' OR [OrderCategory]='REWORK' OR [OrderCategory]='INSURANCE_REPAIR' OR [OrderCategory]='QUICK_SERVICE' OR [OrderCategory]='GENERAL_REPAIR' OR [OrderCategory]='MAINTENANCE')");
            });

            entity.HasOne(d => d.Appointment).WithMany(p => p.ServiceOrders)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK__ServiceOr__Appoi__53385258");

            entity.HasOne(d => d.GroupSession).WithMany(p => p.ServiceOrders)
                .HasForeignKey(d => d.GroupSessionId)
                .HasConstraintName("FK_ServiceOrders_GroupSessionId");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceOrderCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__ServiceOr__Creat__59E54FE7");

            entity.HasOne(d => d.Customer).WithMany(p => p.ServiceOrders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Custo__515009E6");

            entity.HasOne(d => d.ExecuteBranch).WithMany(p => p.ServiceOrderExecuteBranches)
                .HasForeignKey(d => d.ExecuteBranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Execu__55209ACA");

            entity.HasOne(d => d.OriginBranch).WithMany(p => p.ServiceOrderOriginBranches)
                .HasForeignKey(d => d.OriginBranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Origi__542C7691");

            entity.HasOne(d => d.OriginalServiceOrder).WithMany(p => p.InverseOriginalServiceOrder)
                .HasForeignKey(d => d.OriginalServiceOrderId)
                .HasConstraintName("FK__ServiceOr__Origi__5614BF03");

            entity.HasOne(d => d.Qcinspector).WithMany(p => p.ServiceOrderQcinspectors)
                .HasForeignKey(d => d.QcinspectorId)
                .HasConstraintName("FK__ServiceOr__QCIns__58F12BAE");

            entity.HasOne(d => d.Receptionist).WithMany(p => p.ServiceOrderReceptionists)
                .HasForeignKey(d => d.ReceptionistId)
                .HasConstraintName("FK__ServiceOr__Recep__57FD0775");

            entity.HasOne(d => d.ServiceAdvisor).WithMany(p => p.ServiceOrderServiceAdvisors)
                .HasForeignKey(d => d.ServiceAdvisorId)
                .HasConstraintName("FK__ServiceOr__Servi__5708E33C");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceOrderUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__ServiceOr__Updat__5AD97420");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.ServiceOrders)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Vehic__52442E1F");
        });

        modelBuilder.Entity<ServiceOrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceO__3214EC07F58DFBE0");
            entity.Ignore("DiagnosisRecommendationId");

            entity.Property(e => e.BuyoutCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CostPriceAtTime).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CustomerCoPayAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountAllocatedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
#pragma warning disable CS0618
            entity.Property(e => e.DiscountValue)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
#pragma warning restore CS0618
            entity.Property(e => e.EstimatedMinutes);
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GarageAbsorbedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceApprovedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceRequestedAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderDetailInsuranceStatusCode.NOT_APPLICABLE));
            entity.Property(e => e.IsCustomerBroughtPart).HasDefaultValue(false);
            entity.Property(e => e.IsReturnOldPart).HasDefaultValue(false);
            entity.Property(e => e.IsSupplementary).HasDefaultValue(false);
            entity.Property(e => e.IsWarrantyItem).HasDefaultValue(false);
            entity.Property(e => e.IssuedQuantity)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ItemName).HasMaxLength(255);
            entity.Property(e => e.ItemType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.PayerType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ServiceSource)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderDetailServiceSourceCode.INITIAL));
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceOrderDetailStatusCode.DRAFT));
            entity.Property(e => e.SupplementaryReasonCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SupplementaryRequestedAt).HasColumnType("datetime2");
            entity.Property(e => e.CustomerApprovedAt).HasColumnType("datetime2");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxRate).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UnitPriceAtTime).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.ToTable(tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_ServiceOrderDetails_SupplementaryReasonCode",
                    "[SupplementaryReasonCode] IS NULL OR [SupplementaryReasonCode] IN ('TECH_FINDING','CUSTOMER_REQUEST')");

                tableBuilder.HasCheckConstraint(
                    "CK_ServiceOrderDetails_SupplementaryConsistency",
                    "([ServiceSource]='INITIAL' AND [IsSupplementary]=0 AND [SupplementaryReasonCode] IS NULL) OR " +
                    "([ServiceSource]='SUPPLEMENTARY_TECHNICIAN' AND [IsSupplementary]=1 AND [SupplementaryReasonCode]='TECH_FINDING') OR " +
                    "([ServiceSource]='SUPPLEMENTARY_CUSTOMER' AND [IsSupplementary]=1 AND [SupplementaryReasonCode]='CUSTOMER_REQUEST')");
            });

            entity.HasOne(d => d.AbsorbedApprovedByNavigation).WithMany(p => p.ServiceOrderDetailAbsorbedApprovedByNavigations)
                .HasForeignKey(d => d.AbsorbedApprovedBy)
                .HasConstraintName("FK__ServiceOr__Absor__43F60EC8");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceOrderDetailCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__ServiceOr__Creat__44EA3301");

            entity.HasOne(d => d.QuotationDetail).WithMany(p => p.ServiceOrderDetails)
                .HasForeignKey(d => d.QuotationDetailId)
                .HasConstraintName("FK_ServiceOrderDetails_QuotationDetails");

            entity.HasOne(d => d.SourceQuotation).WithMany()
                .HasForeignKey(d => d.SourceQuotationId)
                .HasConstraintName("FK_ServiceOrderDetails_SourceQuotation");

            entity.HasOne(d => d.InsuranceCompany).WithMany(p => p.ServiceOrderDetails)
                .HasForeignKey(d => d.InsuranceCompanyId)
                .HasConstraintName("FK__ServiceOr__Insur__4C8B54C9");

            entity.HasOne(d => d.OriginalOrderDetail).WithMany(p => p.InverseOriginalOrderDetail)
                .HasForeignKey(d => d.OriginalOrderDetailId)
                .HasConstraintName("FK__ServiceOr__Origi__4AA30C57");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.ServiceOrderDetails)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Servi__46D27B73");

            entity.HasOne(d => d.StandardService).WithMany(p => p.ServiceOrderDetails)
                .HasForeignKey(d => d.StandardServiceId)
                .HasConstraintName("FK__ServiceOr__Stand__48BAC3E5");

            entity.HasOne(d => d.Supplier).WithMany(p => p.ServiceOrderDetails)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK__ServiceOr__Suppl__4B973090");

            entity.HasOne(d => d.Technician).WithMany(p => p.ServiceOrderDetailTechnicians)
                .HasForeignKey(d => d.TechnicianId)
                .HasConstraintName("FK__ServiceOr__Techn__49AEE81E");

            entity.HasOne(d => d.SupplementaryRequestedByStaff).WithMany(p => p.ServiceOrderDetailSupplementaryRequestedByStaffNavigations)
                .HasForeignKey(d => d.SupplementaryRequestedByStaffId)
                .HasConstraintName("FK_ServiceOrderDetails_SupplementaryRequestedByStaff");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceOrderDetailUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__ServiceOr__Updat__45DE573A");
        });

        modelBuilder.Entity<ServiceOrderDiscount>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceO__3214EC07B07AEE3B");

            entity.HasIndex(e => new { e.ServiceOrderId, e.DiscountCategory }, "UQ_ServiceOrder_DiscountCategory").IsUnique();

            entity.Property(e => e.AppliedAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountCategory).HasComputedColumnSql("(case when [DiscountType]='MEMBERSHIP' then (1) when [DiscountType]='MANUAL' OR [DiscountType]='VOUCHER' then (2)  end)", true);
            entity.Property(e => e.DiscountType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ValueType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.AppliedByNavigation).WithMany(p => p.ServiceOrderDiscounts)
                .HasForeignKey(d => d.AppliedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Appli__4F67C174");

            entity.HasOne(d => d.CustomerTier).WithMany(p => p.ServiceOrderDiscounts)
                .HasForeignKey(d => d.CustomerTierId)
                .HasConstraintName("FK__ServiceOr__Custo__4E739D3B");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.ServiceOrderDiscounts)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Servi__4D7F7902");

            entity.HasOne(d => d.Voucher).WithMany(p => p.ServiceOrderDiscounts)
                .HasForeignKey(d => d.VoucherId)
                .HasConstraintName("FK_ServiceOrderDiscounts_VoucherId");
        });

        modelBuilder.Entity<ServiceOrderInsuranceAsset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ServiceOrderInsuranceAssets");

            entity.HasIndex(e => new { e.ServiceOrderId, e.AssetType, e.SortOrder }, "IX_ServiceOrderInsuranceAssets_ServiceOrder_Type_Order");

            entity.Property(e => e.AssetType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.InsuranceAssets)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ServiceOrderInsuranceAssets_ServiceOrders");
        });

        modelBuilder.Entity<ServiceOrderInsuranceUpload>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ServiceOrderInsuranceUploads");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_ServiceOrderInsuranceUploads_Status_CreatedAt");

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

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.InsuranceUploads)
                .HasForeignKey(d => d.ServiceOrderId)
                .HasConstraintName("FK_ServiceOrderInsuranceUploads_ServiceOrders");
        });

        modelBuilder.Entity<ServiceOrderSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceO__3214EC0711723175");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceOrderSnapshots)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__ServiceOr__Creat__5CC1BC92");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.ServiceOrderSnapshots)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceOr__Servi__5BCD9859");
        });

        modelBuilder.Entity<ServiceRefund>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceR__3214EC07E092AD2D");

            entity.Property(e => e.ApprovalNote).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RefundAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceRefundStatusCode.PENDING));

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.ServiceRefundApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__ServiceRe__Appro__5EAA0504");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceRefundCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceRe__Creat__5F9E293D");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.ServiceRefunds)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceRe__Servi__5DB5E0CB");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceRefundUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__ServiceRe__Updat__60924D76");
        });

        modelBuilder.Entity<ServiceStaffAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceS__3214EC074773E670");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.EstimatedCommission).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsRework).HasDefaultValue(false);
            entity.Property(e => e.RoleInTask)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SplitRatio)
                .HasDefaultValue(100.00m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.RequiresInsuranceProof).HasDefaultValue(false);
            entity.Property(e => e.ProofImageUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(ServiceProgressStatus.PENDING));

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.ServiceStaffAssignmentAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .HasConstraintName("FK__ServiceSt__Assig__627A95E8");

            entity.HasOne(d => d.ServiceOrderDetail).WithMany(p => p.ServiceStaffAssignments)
                .HasForeignKey(d => d.ServiceOrderDetailId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceSt__Servi__618671AF");

            entity.HasOne(d => d.Technician).WithMany(p => p.ServiceStaffAssignmentTechnicians)
                .HasForeignKey(d => d.TechnicianId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ServiceSt__Techn__636EBA21");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceStaffAssignmentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__ServiceSt__Updat__6462DE5A");
        });
    }
}
