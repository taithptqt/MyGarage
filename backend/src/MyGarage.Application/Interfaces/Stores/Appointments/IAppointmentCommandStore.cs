using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentCommandStore
{
    Task ExecuteInTransactionAsync(Func<Task> action);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action);
    Task SaveChangesInTransactionAsync();

    void AddAppointment(Appointment appointment);
    void AddCustomer(Customer customer);
    void AddVehicle(Vehicle vehicle);
    void AddCustomerVehicle(CustomerVehicle link);
    void AddWorkSlot(WorkSlot slot);
    void AddWorkSlots(IEnumerable<WorkSlot> slots);
    void AddPartInventoryReservation(PartInventoryReservation reservation);
    void AddAppointmentInsuranceAssets(IEnumerable<AppointmentInsuranceAsset> assets);
    void AddServiceOrderInsuranceAssets(IEnumerable<ServiceOrderInsuranceAsset> assets);
    void AddServiceOrderDetails(IEnumerable<ServiceOrderDetail> details);
    void AddNotification(Notification notification);
    void AddAiSmartDispatching(AiSmartDispatching dispatching);
}
