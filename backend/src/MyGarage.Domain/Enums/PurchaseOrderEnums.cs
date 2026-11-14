namespace MyGarage.Domain.Enums;

public enum PurchaseOrderStatus
{
    DRAFT,
    PENDING_APPROVAL,
    APPROVED,
    PARTIAL_RECEIVED,
    COMPLETED,
    CANCELED
}

public enum GoodsReceiptStatus
{
    DRAFT,
    POSTED,
    CANCELED
}
