using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderIntakeCommandStore
{
    void AddVehicleConditionLog(VehicleConditionLog log);
    void AddCustomer(Customer customer);
    void AddVehicle(Vehicle vehicle);
    void AddCustomerVehicle(CustomerVehicle link);
}
