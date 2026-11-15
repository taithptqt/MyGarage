namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderCommandStore :
    IServiceOrderTransactionCommandStore,
    IServiceOrderCreationCommandStore,
    IServiceOrderBillingCommandStore,
    IServiceOrderCommissionCommandStore,
    IServiceOrderIntakeCommandStore
{
}
