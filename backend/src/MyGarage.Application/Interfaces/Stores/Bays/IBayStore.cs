namespace MyGarage.Application.Bays;

public interface IBayStore :
    IBayQueryStore,
    IBayCommandStore,
    IBayAllocationStore
{
}
