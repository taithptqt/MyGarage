namespace MyGarage.Application.Purchasing;

public interface IPurchaseOrderStore :
    IPurchaseOrderTransactionStore,
    IPurchaseOrderQueryStore,
    IPurchaseOrderCommandStore,
    IPurchaseOrderGoodsReceiptStore
{
}
