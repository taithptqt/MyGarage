namespace MyGarage.Domain.Enums;

public enum StockTransactionType
{
    IN,
    OUT,
    TRANSFER,
    ADJUSTMENT
}

public enum StockAlertLevel
{
    OutOfStock,
    BelowMinStock,
    BelowReorder
}

public enum MaterialRequestStatusCode
{
    PENDING,
    PARTIALLY_ISSUED,
    ISSUED,
    REJECTED,
    CANCELED
}

public enum ItemConditionCode
{
    GOOD,
    DAMAGED,
    DEFECTIVE
}
