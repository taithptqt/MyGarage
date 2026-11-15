using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderCommissionCommandStore
{
    void AddCommissionTransaction(CommissionTransaction tx);
}
