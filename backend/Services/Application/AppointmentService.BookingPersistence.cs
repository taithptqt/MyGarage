using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
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
            long? groupSessionId,
            DateTime requestedStartAt,
            DateTime requestedEndAt,
            BookingFlowType flow,
            string? effectiveRequestedServiceIds,
            string? effectiveRequestedPackageIds,
            ServiceCluster requestedCluster,
            long? validatedVehicleModelId,
            int plannedDurationMinutes,
            int slotStepMinutes,
            PartAvailabilityEvaluation partAvailability)
        {
            var ownTransaction = externalTransaction == null;

            if (ownTransaction)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    using var tx = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var result = await PersistBookingInternalAsync(
                            actorStaffId, dto, actualCustomerId, groupSessionId, 
                            requestedStartAt, requestedEndAt, flow, 
                            effectiveRequestedServiceIds, effectiveRequestedPackageIds, 
                            requestedCluster, validatedVehicleModelId, 
                            plannedDurationMinutes, slotStepMinutes, partAvailability);
                        await tx.CommitAsync();
                        return result;
                    }
                    catch
                    {
                        await tx.RollbackAsync();
                        throw;
                    }
                });
            }
            else
            {
                return await PersistBookingInternalAsync(
                    actorStaffId, dto, actualCustomerId, groupSessionId, 
                    requestedStartAt, requestedEndAt, flow, 
                    effectiveRequestedServiceIds, effectiveRequestedPackageIds, 
                    requestedCluster, validatedVehicleModelId, 
                    plannedDurationMinutes, slotStepMinutes, partAvailability);
            }
        }

        private async Task<Appointment> PersistBookingInternalAsync(
            long? actorStaffId,
            BookAppointmentRequestDto dto,
            long actualCustomerId,
            long? groupSessionId,
            DateTime requestedStartAt,
            DateTime requestedEndAt,
            BookingFlowType flow,
            string? effectiveRequestedServiceIds,
            string? effectiveRequestedPackageIds,
            ServiceCluster requestedCluster,
            long? validatedVehicleModelId,
            int plannedDurationMinutes,
            int slotStepMinutes,
            PartAvailabilityEvaluation partAvailability)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == actualCustomerId);
            if (customer == null) throw new InvalidOperationException("Không tìm thấy khách hàng để đặt lịch.");

            await ValidateSelectedVehicleOwnershipAsync(actualCustomerId, dto.VehicleId);

            UpdateCustomerContact(customer, dto);
            var vehicleId = await ResolveVehicleForBookingAsync(actualCustomerId, actorStaffId, dto);

            var appointment = new Appointment
            {
                BranchId = dto.BranchId,
                CustomerId = actualCustomerId,
                VehicleId = vehicleId,
                GroupSessionId = groupSessionId,
                Status = nameof(AppointmentStatus.PENDING),
                CreatedBy = actorStaffId,
                CustomerNotes = dto.CustomerNotes,
                StartTime = requestedStartAt,
                EndTime = requestedEndAt,
                RequestedServiceIds = effectiveRequestedServiceIds,
                RequestedPackageIds = effectiveRequestedPackageIds,
                InsuranceCompanyId = dto.InsuranceCompanyId.HasValue && dto.InsuranceCompanyId.Value > 0
                    ? dto.InsuranceCompanyId
                    : null,
                InsuranceDeductibleAmount = NormalizeInsuranceDeductibleAmount(dto.InsuranceDeductibleAmount),
                InsuranceDeductibleOption = NormalizeInsuranceToken(dto.InsuranceDeductibleOption),
                InsuranceClaimMode = NormalizeInsuranceToken(dto.InsuranceClaimMode),
                InsuranceQuoteState = NormalizeInsuranceToken(dto.InsuranceQuoteState),
                CreatedAt = _clock.LocalNow,
                UpdatedAt = _clock.LocalNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            await PersistAppointmentInsuranceAssetsAsync(appointment, dto);

            var effectiveVehicleModelId = validatedVehicleModelId;
            if (!effectiveVehicleModelId.HasValue && vehicleId.HasValue)
            {
                effectiveVehicleModelId = await _context.Vehicles
                    .AsNoTracking()
                    .Where(v => v.Id == vehicleId.Value)
                    .Select(v => (long?)v.ModelId)
                    .FirstOrDefaultAsync();
            }

            await ApplyBookingReservationArtifactsAsync(
                appointment,
                flow,
                requestedCluster,
                effectiveVehicleModelId,
                effectiveRequestedServiceIds,
                effectiveRequestedPackageIds,
                actorStaffId,
                plannedDurationMinutes,
                slotStepMinutes,
                partAvailability);

            if (groupSessionId.HasValue)
                await _groupSessionService.SyncCountersAsync(groupSessionId.Value, actorStaffId);

            return appointment;
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

        private static decimal? NormalizeInsuranceDeductibleAmount(decimal? value)
        {
            if (!value.HasValue) return null;
            if (value.Value < 0m) throw new InvalidOperationException("Mức miễn thường không hợp lệ.");
            return decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero);
        }

        private static string? NormalizeInsuranceToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Trim().ToUpperInvariant();
            return normalized.Length <= 30 ? normalized : normalized.Substring(0, 30);
        }
    }
}
