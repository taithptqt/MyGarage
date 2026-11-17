using System.Collections.Generic;
using MyGarage.Application.Payments.DTOs;

namespace MyGarage.Interfaces
{
    public interface IOrderTransactionStore
    {
        List<OrderTransaction> GetAllTransactions();
        OrderTransaction? GetTransactionById(int id);
        List<OrderTransaction> GetTransactionsByOrderId(int orderId);
        List<OrderTransaction> GetTransactionsByOrderCode(long orderCode);
        List<OrderTransaction> GetTransactionsByPaymentLinkId(string paymentLinkId);
        void CreateTransaction(OrderTransaction transaction);
        void CreateTransactions(List<OrderTransaction> transactions);
        bool UpdateTransaction(int id, OrderTransaction updatedTransaction);
        bool DeleteTransaction(int id);
        bool DeleteTransactionsByOrderId(int orderId);
    }
}
