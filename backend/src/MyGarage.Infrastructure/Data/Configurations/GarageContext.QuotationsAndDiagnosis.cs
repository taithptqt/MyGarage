using System;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigureQuotationsAndDiagnosis(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DiagnosisRecommendation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Diagnose__3214EC070DEF315F");
            entity.ToTable("DiagnosisRecommendations");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ItemType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Urgency)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DiagnosisRecommendationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__DiagnoseR__Creat__5DEAEAF5");

            entity.HasOne(d => d.Diagnosis).WithMany(p => p.DiagnosisRecommendations)
                .HasForeignKey(d => d.DiagnosisId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DiagnoseR__Diagn__5A1A5A11");

            entity.HasOne(d => d.DiagnosisAssignment).WithMany(p => p.DiagnosisRecommendations)
                .HasForeignKey(d => d.DiagnosisAssignmentId)
                .HasConstraintName("FK_DiagnosisRecommendations_DiagnosisAssignments");

            entity.HasOne(d => d.InventoryItem).WithMany(p => p.DiagnosisRecommendations)
                .HasForeignKey(d => d.InventoryItemId)
                .HasConstraintName("FK__DiagnoseR__Inven__5CF6C6BC");

            entity.HasOne(d => d.ParentRecommendation).WithMany(p => p.InverseParentRecommendation)
                .HasForeignKey(d => d.ParentRecommendationId)
                .HasConstraintName("FK__DiagnoseR__Paren__5B0E7E4A");

            entity.HasOne(d => d.StandardService).WithMany(p => p.DiagnosisRecommendations)
                .HasForeignKey(d => d.StandardServiceId)
                .HasConstraintName("FK__DiagnoseR__Stand__5C02A283");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DiagnosisRecommendationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__DiagnoseR__Updat__5EDF0F2E");
        });

        modelBuilder.Entity<DiagnosisAssignment>(entity =>
        {
            entity.ToTable("DiagnosisAssignments");

            entity.HasKey(e => e.Id).HasName("PK_DiagnosisAssignments");

            entity.Property(e => e.EventType)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.OldStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CancellationStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.CancellationDecisionNote).HasMaxLength(500);
            entity.Property(e => e.SupportRequestNote).HasMaxLength(500);
            entity.Property(e => e.TeamLeaderApprovalStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.FinalApprovalStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ForemanRejectReason).HasMaxLength(500);
            entity.Property(e => e.TeamLeaderNote).HasMaxLength(1000);
            entity.Property(e => e.ForemanNote).HasMaxLength(1000);
            entity.Property(e => e.RequiresInsuranceProof).HasDefaultValue(false);
            entity.Property(e => e.ProofImageUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Diagnosis).WithMany(p => p.DiagnosisAssignments)
                .HasForeignKey(d => d.DiagnosisId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_DiagnosisAssignments_Diagnoses");
        });

        modelBuilder.Entity<Diagnosis>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Diagnose__3214EC071BC3DD6E");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DispatchTag)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Severity)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue(nameof(DiagnosisStatus.PENDING));
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.Diagnoses)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Diagnoses__Servi__5FD33367");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DiagnosisUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__Diagnoses__Updat__62AFA012");
        });

        modelBuilder.Entity<QcInspection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__QC_Inspe__3214EC07E74328AB");

            entity.ToTable("QC_Inspections");

            entity.Property(e => e.FailReason).HasMaxLength(500);
            entity.Property(e => e.OverallScore).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.QcStaffId).HasColumnName("QC_StaffId");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.QcStaff).WithMany(p => p.QcInspectionQcStaffs)
                .HasForeignKey(d => d.QcStaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QC_Inspec__QC_St__2942188C");

            entity.HasOne(d => d.ReworkTechnician).WithMany(p => p.QcInspectionReworkTechnicians)
                .HasForeignKey(d => d.ReworkTechnicianId)
                .HasConstraintName("FK__QC_Inspec__Rewor__2B2A60FE");

            entity.HasOne(d => d.ServiceOrderDetail).WithMany(p => p.QcInspections)
                .HasForeignKey(d => d.ServiceOrderDetailId)
                .HasConstraintName("FK__QC_Inspec__Servi__2A363CC5");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.QcInspections)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__QC_Inspec__Servi__284DF453");
        });

        modelBuilder.Entity<Quotation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Quotations");
            entity.ToTable("Quotations", t =>
                t.HasCheckConstraint(
                    "CK_Quotations_InsuranceApprovalEvidence_Required",
                    "[Status] <> 'INSURANCE_APPROVED' OR [TotalInsuranceAmount] <= 0 OR NULLIF(LTRIM(RTRIM([InsuranceApprovalEvidenceUrl])), '') IS NOT NULL"));

            entity.HasIndex(e => e.Code, "UQ_Quotations_Code").IsUnique();

            entity.HasIndex(e => new { e.ServiceOrderId, e.VersionNo }, "IX_Quotations_ServiceOrderId_VersionNo").IsUnique();

            entity.HasIndex(e => new { e.ServiceOrderId, e.Status, e.CreatedAt }, "IX_Quotations_ServiceOrderId_Status_CreatedAt");
            entity.HasIndex(e => e.DiagnosisId, "IX_Quotations_DiagnosisId");
            entity.HasIndex(e => e.DiagnosisAssignmentId, "IX_Quotations_DiagnosisAssignmentId");
            entity.HasIndex(e => e.InsuranceChangeType, "IX_Quotations_InsuranceChangeType");

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.QuotationType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("NORMAL");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.SubTotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
#pragma warning disable CS0618
            entity.Ignore(e => e.TotalDiscount);
#pragma warning restore CS0618
            entity.Property(e => e.TaxAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalInsuranceAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalCustomerAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceChangeType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("NONE");
            // Hotfix for environments where the column is not deployed yet.
            entity.Ignore(e => e.InsuranceClaimNumber);
            entity.Property(e => e.InsuranceApprovalEvidenceUrl)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.CustomerNotes).HasMaxLength(1000);
            entity.Property(e => e.InternalNotes).HasMaxLength(1000);
            entity.Property(e => e.TechnicalApprovalStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.RequiresFinancialApproval).HasDefaultValue(false);
            entity.Property(e => e.FinancialApprovalStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("NOT_REQUIRED");
            entity.Property(e => e.IsAppliedToServiceOrder).HasDefaultValue(false);
            entity.Property(e => e.IsReadyForSplitBilling).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.Quotations)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Quotations_ServiceOrders");

            entity.HasOne(d => d.Diagnosis).WithMany(p => p.Quotations)
                .HasForeignKey(d => d.DiagnosisId)
                .HasConstraintName("FK_Quotations_Diagnoses");

            entity.HasOne(d => d.DiagnosisAssignment).WithMany(p => p.Quotations)
                .HasForeignKey(d => d.DiagnosisAssignmentId)
                .HasConstraintName("FK_Quotations_DiagnosisAssignments");

            entity.HasOne(d => d.ServiceAdvisor).WithMany(p => p.QuotationServiceAdvisors)
                .HasForeignKey(d => d.ServiceAdvisorId)
                .HasConstraintName("FK_Quotations_ServiceAdvisor");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.QuotationApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_Quotations_ApprovedBy");

            entity.HasOne(d => d.AppliedByNavigation).WithMany(p => p.QuotationAppliedByNavigations)
                .HasForeignKey(d => d.AppliedBy)
                .HasConstraintName("FK_Quotations_AppliedBy");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.QuotationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_Quotations_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.QuotationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_Quotations_UpdatedBy");
        });

        modelBuilder.Entity<QuotationDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_QuotationDetails");

            entity.HasIndex(e => new { e.QuotationId, e.ApprovalStatus }, "IX_QuotationDetails_QuotationId_ApprovalStatus");

            entity.HasIndex(e => e.SourceQuotationDetailId, "IX_QuotationDetails_SourceQuotationDetailId");
            entity.HasIndex(e => e.DiagnosisRecommendationId, "IX_QuotationDetails_DiagnosisRecommendationId");

            entity.Property(e => e.ItemType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ItemName).HasMaxLength(255);
            entity.Property(e => e.PayerType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ApprovalStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UnitPriceAtTime).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CostPriceAtTime).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
#pragma warning disable CS0618
            entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(7, 4)");
            entity.Property(e => e.DiscountValue)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
#pragma warning restore CS0618
            entity.Property(e => e.TaxRate).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceCoveredAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CustomerCoveredAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsBlocking).HasDefaultValue(true);
            entity.Property(e => e.IsReturnOldPart).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Quotation).WithMany(p => p.QuotationDetails)
                .HasForeignKey(d => d.QuotationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_QuotationDetails_Quotations");

            entity.HasOne(d => d.ServiceOrder).WithMany(p => p.QuotationDetails)
                .HasForeignKey(d => d.ServiceOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QuotationDetails_ServiceOrders");

            entity.HasOne<QuotationDetail>()
                .WithMany()
                .HasForeignKey(d => d.SourceQuotationDetailId)
                .HasConstraintName("FK_QuotationDetails_SourceQuotationDetail");

            entity.HasOne(d => d.DiagnosisRecommendation).WithMany()
                .HasForeignKey(d => d.DiagnosisRecommendationId)
                .HasConstraintName("FK_QuotationDetails_DiagnosisRecommendations");

            entity.HasOne(d => d.InsuranceCompany).WithMany()
                .HasForeignKey(d => d.InsuranceCompanyId)
                .HasConstraintName("FK_QuotationDetails_InsuranceCompanies");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.QuotationDetailApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_QuotationDetails_ApprovedBy");

            entity.HasOne(d => d.CanceledByNavigation).WithMany()
                .HasForeignKey(d => d.CanceledBy)
                .HasConstraintName("FK_QuotationDetails_CanceledBy");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.QuotationDetailCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_QuotationDetails_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.QuotationDetailUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_QuotationDetails_UpdatedBy");
        });

        modelBuilder.Entity<QuotationDiscount>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_QuotationDiscounts");

            entity.HasIndex(e => e.QuotationId, "IX_QuotationDiscounts_QuotationId");
            entity.HasIndex(e => e.AppliedBy, "IX_QuotationDiscounts_AppliedBy");
            entity.HasIndex(e => e.CustomerTierId, "IX_QuotationDiscounts_CustomerTierId");
            entity.HasIndex(e => e.VoucherId, "IX_QuotationDiscounts_VoucherId");
            entity.HasIndex(e => new { e.QuotationId, e.DiscountType }, "UQ_QuotationDiscounts_Quotation_DiscountType")
                .IsUnique()
                .HasFilter("[QuotationId] IS NOT NULL AND [DiscountType] IS NOT NULL");

            entity.Property(e => e.DiscountType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ValueType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.AppliedAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DiscountCategory)
                .ValueGeneratedOnAddOrUpdate()
                .HasComputedColumnSql("(case when [DiscountType]='MEMBERSHIP' then (1) when [DiscountType]='MANUAL' OR [DiscountType]='VOUCHER' then (2) end)", true);

            entity.HasOne(d => d.Quotation).WithMany(p => p.QuotationDiscounts)
                .HasForeignKey(d => d.QuotationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_QuotationDiscounts_QuotationId");

            entity.HasOne(d => d.AppliedByNavigation).WithMany(p => p.QuotationDiscounts)
                .HasForeignKey(d => d.AppliedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QuotationDiscounts_AppliedBy");

            entity.HasOne(d => d.CustomerTier).WithMany(p => p.QuotationDiscounts)
                .HasForeignKey(d => d.CustomerTierId)
                .HasConstraintName("FK_QuotationDiscounts_CustomerTierId");

            entity.HasOne(d => d.Voucher).WithMany(p => p.QuotationDiscounts)
                .HasForeignKey(d => d.VoucherId)
                .HasConstraintName("FK_QuotationDiscounts_VoucherId");
        });

        modelBuilder.Entity<QuotationAccessLink>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_QuotationAccessLinks");

            entity.HasIndex(e => e.TokenHash, "UQ_QuotationAccessLinks_TokenHash").IsUnique();
            entity.HasIndex(e => new { e.QuotationId, e.Status }, "IX_QuotationAccessLinks_QuotationId_Status");
            entity.HasIndex(e => e.ExpiresAt, "IX_QuotationAccessLinks_ExpiresAt");

            entity.Property(e => e.TokenHash)
                .HasMaxLength(64)
                .IsUnicode(false);

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");

            entity.Property(e => e.DecisionCode)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.SentToEmail).HasMaxLength(255);
            entity.Property(e => e.ClientNote).HasMaxLength(1000);
            entity.Property(e => e.OtpCodeHash)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.OtpAttemptCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Quotation).WithMany(p => p.QuotationAccessLinks)
                .HasForeignKey(d => d.QuotationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_QuotationAccessLinks_Quotations");
        });

        modelBuilder.Entity<QuotationApprovalAction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_QuotationApprovalActions");

            entity.HasIndex(e => new { e.QuotationId, e.ApprovalType, e.CreatedAt }, "IX_QuotationApprovalActions_QuotationId_ApprovalType_CreatedAt");

            entity.Property(e => e.ApprovalType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Decision)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Quotation).WithMany(p => p.QuotationApprovalActions)
                .HasForeignKey(d => d.QuotationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_QuotationApprovalActions_Quotations");
        });

        modelBuilder.Entity<ServiceCategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ServiceC__3214EC07586F8C98");

            entity.HasIndex(e => e.Code, "UQ__ServiceC__A25C5AA76FAA12AA").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceCategoryCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__ServiceCa__Creat__40257DE4");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.ServiceCategoryDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__ServiceCa__Delet__420DC656");

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .HasForeignKey(d => d.ParentCategoryId)
                .HasConstraintName("FK__ServiceCa__Paren__4301EA8F");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceCategoryUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__ServiceCa__Updat__4119A21D");
        });

        modelBuilder.Entity<StandardService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Standard__3214EC0745B61617");

            entity.HasIndex(e => e.Code, "UQ__Standard__A25C5AA70956C7EB").IsUnique();

            entity.Property(e => e.BasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ConsumableCost)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.TaxRate)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.WarrantyKm).HasDefaultValue(0);
            entity.Property(e => e.WarrantyMonths).HasDefaultValue(0);

            entity.HasOne(d => d.Category).WithMany(p => p.StandardServices)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StandardS__Categ__74994623");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StandardServiceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__StandardS__Creat__758D6A5C");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.StandardServiceDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK__StandardS__Delet__7775B2CE");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.StandardServiceUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__StandardS__Updat__76818E95");
        });

        modelBuilder.Entity<ServiceBom>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ServiceBOMs");

            entity.ToTable("ServiceBOMs");

            entity.Property(e => e.CreateAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsMandatory).HasDefaultValue(true);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.UpdateAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CreateByNavigation).WithMany()
                .HasForeignKey(d => d.CreateBy)
                .HasConstraintName("FK_ServiceBOMs_CreateBy");

            entity.HasOne(d => d.Product).WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceBOMs_Product");

            entity.HasOne(d => d.Service).WithMany()
                .HasForeignKey(d => d.ServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceBOMs_Service");

            entity.HasOne(d => d.UpdateByNavigation).WithMany()
                .HasForeignKey(d => d.UpdateBy)
                .HasConstraintName("FK_ServiceBOMs_UpdateBy");

            entity.HasOne(d => d.VehicleModel).WithMany()
                .HasForeignKey(d => d.VehicleModelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceBOMs_VehicleModel");
        });
    }
}
