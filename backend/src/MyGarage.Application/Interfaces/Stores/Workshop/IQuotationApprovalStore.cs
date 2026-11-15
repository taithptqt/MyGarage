namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IQuotationApprovalStore
    : IQuotationApprovalDiscountStore,
      IQuotationApprovalPolicyStore,
      IQuotationApprovalOrderSyncStore
{
}
