using MyGarage.Application.Payments.DTOs;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class OrderTransactionService : IOrderTransactionStore
{
    private readonly List<OrderTransaction> _transactions = [];
    private int _nextId = 1;

    public List<OrderTransaction> GetAllTransactions()
    {
        return _transactions;
    }

    public OrderTransaction? GetTransactionById(int id)
    {
        return _transactions.FirstOrDefault(t => t.Id == id);
    }

    public List<OrderTransaction> GetTransactionsByOrderId(int orderId)
    {
        return _transactions.Where(t => t.OrderId == orderId).ToList();
    }

    public List<OrderTransaction> GetTransactionsByOrderCode(long orderCode)
    {
        return _transactions.Where(t => t.OrderCode == orderCode).ToList();
    }

    public List<OrderTransaction> GetTransactionsByPaymentLinkId(string paymentLinkId)
    {
        return _transactions.Where(t => t.PaymentLinkId == paymentLinkId).ToList();
    }

    public void CreateTransaction(OrderTransaction transaction)
    {
        transaction.Id = _nextId++;
        _transactions.Add(transaction);
    }

    public void CreateTransactions(List<OrderTransaction> transactions)
    {
        foreach (var transaction in transactions)
        {
            CreateTransaction(transaction);
        }
    }

    public bool UpdateTransaction(int id, OrderTransaction updatedTransaction)
    {
        var transaction = _transactions.FirstOrDefault(t => t.Id == id);
        if (transaction == null) return false;

        transaction.OrderId = updatedTransaction.OrderId;
        transaction.OrderCode = updatedTransaction.OrderCode;
        transaction.PaymentLinkId = updatedTransaction.PaymentLinkId;
        transaction.Reference = updatedTransaction.Reference;
        transaction.Amount = updatedTransaction.Amount;
        transaction.AccountNumber = updatedTransaction.AccountNumber;
        transaction.Description = updatedTransaction.Description;
        transaction.TransactionDateTime = updatedTransaction.TransactionDateTime;
        transaction.VirtualAccountName = updatedTransaction.VirtualAccountName;
        transaction.VirtualAccountNumber = updatedTransaction.VirtualAccountNumber;
        transaction.CounterAccountBankId = updatedTransaction.CounterAccountBankId;
        transaction.CounterAccountBankName = updatedTransaction.CounterAccountBankName;
        transaction.CounterAccountName = updatedTransaction.CounterAccountName;
        transaction.CounterAccountNumber = updatedTransaction.CounterAccountNumber;

        return true;
    }

    public bool DeleteTransaction(int id)
    {
        var transaction = _transactions.FirstOrDefault(t => t.Id == id);
        if (transaction == null) return false;
        _transactions.Remove(transaction);
        return true;
    }

    public bool DeleteTransactionsByOrderId(int orderId)
    {
        var transactionsToRemove = _transactions.Where(t => t.OrderId == orderId).ToList();
        foreach (var transaction in transactionsToRemove)
        {
            _transactions.Remove(transaction);
        }
        return transactionsToRemove.Count > 0;
    }
}

