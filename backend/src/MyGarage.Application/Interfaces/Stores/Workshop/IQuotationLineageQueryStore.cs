using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IQuotationLineageQueryStore
{
    Task<string?> GetStaffEmailAsync(long staffId);
    Task<List<QuotationBranchStaffCandidate>> GetBranchStaffCandidatesAsync(long branchId);
    Task<List<QuotationBaselineRecommendationInfo>> GetBaselineRecommendationsAsync(IReadOnlyList<long> recommendationIds);
    Task<long?> GetServiceOrderExecuteBranchIdAsync(long serviceOrderId);
    Task<BranchQuotationApprovalPolicy?> GetActiveBranchQuotationApprovalPolicyAsNoTrackingAsync(long branchId);
    Task<decimal?> GetPreviousVersionInsuranceAmountAsync(long serviceOrderId, int previousVersionNo);
    Task<QuotationInitialDiagnosisMeta?> GetInitialQuotationDiagnosisMetaAsync(long serviceOrderId);
    Task<QuotationServiceOrderDetailTotals?> GetNonCanceledServiceOrderDetailTotalsAsync(long serviceOrderId, string canceledStatus);
    Task<List<QuotationSodSupplementaryRow>> GetSodSupplementaryRowsAsync(long serviceOrderId);
    Task<List<QuotationLineageRow>> GetQuotationLineageRowsAsync(long serviceOrderId);
    Task<List<QuotationDetailLineageRow>> GetQuotationDetailLineageRowsAsync(long serviceOrderId);
    Task<long?> GetActiveVoucherIdByCodeAsync(string voucherCode);
    Task<QuotationCancellationOrderInfo?> GetCancellationOrderInfoAsync(long serviceOrderId);
}
