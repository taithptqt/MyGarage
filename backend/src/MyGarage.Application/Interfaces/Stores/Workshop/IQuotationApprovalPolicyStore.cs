using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IQuotationApprovalPolicyStore
{
    Task<List<MandatoryOverrideRequest>> GetMandatoryOverrideRequestsByServiceOrderAsync(long serviceOrderId);
    Task<BranchQuotationApprovalPolicy?> GetBranchQuotationApprovalPolicyAsNoTrackingAsync(long branchId);
    Task<QuotationBranchInfo?> GetBranchInfoAsNoTrackingAsync(long branchId);
    Task<BranchQuotationApprovalPolicy?> GetBranchQuotationApprovalPolicyForUpdateAsync(long branchId);
    void AddBranchQuotationApprovalPolicy(BranchQuotationApprovalPolicy entity);
    Task<bool> HasPendingMandatoryOverrideAsync(long quotationId, string approvedStatus);
    void AddMandatoryOverrideRequests(IEnumerable<MandatoryOverrideRequest> rows);
    Task<MandatoryOverrideRequest?> GetMandatoryOverrideRequestForUpdateAsync(long requestId);
}
