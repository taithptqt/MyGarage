using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public sealed record QuotationServiceOrderAdvisorInfo(long Id, long? ServiceAdvisorId, long? ReceptionistId);
public sealed record QuotationDiagnosisAssignmentInfo(long Id, long DiagnosisId, long DiagnosisServiceOrderId);
public sealed record QuotationBranchManagerInfo(long ExecuteBranchId, long? BranchManagerId);
public sealed record QuotationBranchInfo(long Id, long? ManagerId);
public sealed record QuotationBranchStaffCandidate(long Id, string? Email, string? RoleCode, string? RoleName);
public sealed record QuotationBaselineRecommendationInfo(long Id, string? ItemType, long? ItemId, decimal Quantity, string? Urgency);
public sealed record QuotationInitialDiagnosisMeta(long Id, long? DiagnosisAssignmentId);
public sealed record QuotationServiceOrderDetailTotals(decimal SubTotal, decimal Discount, decimal Tax, decimal Final);
public sealed record QuotationSodSupplementaryRow(long QuotationDetailId, bool IsSupplementary, string? ServiceSource, string? SupplementaryReasonCode, long? SourceQuotationId, DateTime? UpdatedAt, long Id);
public sealed record QuotationLineageRow(long Id, int VersionNo, long? DiagnosisAssignmentId);
public sealed record QuotationDetailLineageRow(long Id, long QuotationId, long? SourceQuotationDetailId);
public sealed record QuotationCancellationOrderInfo(long Id, string Code, long? ReceptionistId, long? ServiceAdvisorId);

public interface IQuotationWorkflowStore :
    IQuotationBuildStore,
    IQuotationApprovalStore,
    IQuotationPortalStore,
    IQuotationLineageQueryStore
{
    Task LockServiceOrderForDecisionAsync(long quotationId);
}
