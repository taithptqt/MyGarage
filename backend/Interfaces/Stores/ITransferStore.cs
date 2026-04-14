using System.Collections.Generic;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ITransferStore
    {
        List<Transfer> GetAllTransfers();
        Transfer? GetTransferById(string id);
        void CreateTransfer(Transfer transfer);
        bool UpdateTransfer(string id, Transfer updatedTransfer);
        bool DeleteTransfer(string id);
    }
}
