using MyGarage.Models;
using MyGarage.Interfaces;
using PayOS.Models.V1.Payouts;

namespace MyGarage.Services;

public class TransferService : ITransferStore
{
    private readonly List<Transfer> _transfers = [];

    public List<Transfer> GetAllTransfers()
    {
        return _transfers;
    }

    public Transfer? GetTransferById(string id)
    {
        return _transfers.FirstOrDefault(t => t.Id == id);
    }

    public void CreateTransfer(Transfer transfer)
    {
        _transfers.Add(transfer);
    }

    public bool UpdateTransfer(string id, Transfer updatedTransfer)
    {
        var transfer = _transfers.FirstOrDefault(t => t.Id == id);
        if (transfer == null) return false;
        transfer.Category = updatedTransfer.Category;
        transfer.ApprovalState = updatedTransfer.ApprovalState;
        transfer.CreatedAt = updatedTransfer.CreatedAt;
        transfer.Transactions = updatedTransfer.Transactions;

        return true;
    }

    public bool DeleteTransfer(string id)
    {
        var transfer = _transfers.FirstOrDefault(t => t.Id == id);
        if (transfer == null) return false;
        _transfers.Remove(transfer);
        return true;
    }
}

