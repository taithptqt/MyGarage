namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderQueryStore :
    IServiceOrderStatusQueryStore,
    IServiceOrderWorkflowQueryStore,
    IServiceOrderHandoverQueryStore,
    IServiceOrderBillingQueryStore,
    IServiceOrderIntakeQueryStore,
    IServiceOrderEditCapabilityQueryStore
{
}
