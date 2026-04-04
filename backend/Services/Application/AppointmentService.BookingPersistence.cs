using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<Appointment> PersistBookingAsync(
            IDbContextTransaction? externalTransaction,
            long? actorStaffId,
            BookAppointmentRequestDto dto,
            long actualCustomerId,
            long? bookingGroupId,
            DateTime requestedStartAt,
            DateTime requestedEndAt,
            BookingFlowType flow,
            string? effectiveRequestedServiceIds,
            ServiceCluster requestedCluster)
        {
            var ownTransaction = externalTransaction == null;
            using var tx = ownTransaction ? await _context.Database.BeginTransactionAsync() : null;
            try
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == actualCustomerId);
                if (customer == null) throw new InvalidOperationException("Kh\u00f4ng t\u00ecm th\u1ea5y kh\u00e1ch h\u00e0ng \u0111\u1ec3 \u0111\u1eb7t l\u1ecbch.");

                await ValidateSelectedVehicleOwnershipAsync(actualCustomerId, dto.VehicleId);

                UpdateCustomerContact(customer, dto);
                var vehicleId = await ResolveVehicleForBookingAsync(actualCustomerId, actorStaffId, dto);

                var appointment = new Appointment
                {
                    BranchId = dto.BranchId,
                    CustomerId = actualCustomerId,
                    VehicleId = vehicleId,
                    BookingGroupId = bookingGroupId,
                    CreatedBy = actorStaffId,
                    StartTime = requestedStartAt,
                    EndTime = requestedEndAt,
                    RequestedServiceIds = effectiveRequestedServiceIds,
                    CustomerNotes = AppendClusterMarker(
                        AppendFlowMarker(dto.CustomerNotes, flow),
                        requestedCluster),
                    Status = nameof(AppointmentStatus.PENDING),
                    CreatedAt = _clock.UtcNow
                };
                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                if (bookingGroupId.HasValue)
                    await _bookingGroupService.SyncCountersAsync(bookingGroupId.Value, actorStaffId);

                if (ownTransaction && tx != null)
                    await tx.CommitAsync();
                return appointment;
            }
            catch
            {
                if (ownTransaction && tx != null)
                    await tx.RollbackAsync();
                throw;
            }
        }

        private void UpdateCustomerContact(Customer customer, BookAppointmentRequestDto dto)
        {
            var customerFullName = dto.CustomerFullName?.Trim();
            if (!string.IsNullOrWhiteSpace(customerFullName) &&
                !string.Equals(customer.FullName, customerFullName, StringComparison.Ordinal))
            {
                customer.FullName = customerFullName;
            }

            var customerPhone = dto.CustomerPhone?.Trim();
            if (!string.IsNullOrWhiteSpace(customerPhone) &&
                !string.Equals(customer.Phone, customerPhone, StringComparison.Ordinal))
            {
                customer.Phone = customerPhone;
            }
        }

        private async Task<long?> ResolveVehicleForBookingAsync(
            long customerId,
            long? actorStaffId,
            BookAppointmentRequestDto dto)
        {
            long? vehicleId = dto.VehicleId;
            if (vehicleId != null || string.IsNullOrWhiteSpace(dto.LicensePlate) || !dto.VehicleModelId.HasValue)
                return vehicleId;

            vehicleId = await ResolveOrCreateVehicleAsync(dto, actorStaffId);
            await EnsureCustomerVehicleLinkAsync(customerId, vehicleId.Value, actorStaffId);

            return vehicleId;
        }
    }
}
