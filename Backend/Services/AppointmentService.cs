using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Services
{
    public class AppointmentService : GenericService<Appointment>, IAppointmentService
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly GarageContext _context;

        public AppointmentService(IAppointmentRepository repository, GarageContext context)
            : base(repository)
        {
            _appointmentRepository = repository;
            _context = context;
        }

        public async Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(long branchId, DateOnly date)
        {
            if (date < DateOnly.FromDateTime(DateTime.Today))
                return Enumerable.Empty<AvailableSlotResponseDto>();

            int dayOfWeek = (int)date.DayOfWeek;

            var opHour = await _context.BranchOperatingHours
                .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == dayOfWeek);

            if (opHour == null || opHour.IsDayOff == true)
                return Enumerable.Empty<AvailableSlotResponseDto>();

            var openTime = opHour.OpenTime;
            var closeTime = opHour.CloseTime;

            var exception = await _context.BranchExceptions
                .FirstOrDefaultAsync(e => e.BranchId == branchId && e.ExceptionDate == date);

            if (exception != null)
            {
                if (exception.IsClosed == true)
                    return Enumerable.Empty<AvailableSlotResponseDto>();

                if (exception.StartTime.HasValue)
                    openTime = exception.StartTime.Value;
                if (exception.EndTime.HasValue)
                    closeTime = exception.EndTime.Value;
            }

            var existingAppointments = await _appointmentRepository.GetByBranchAndDateAsync(branchId, date);
            var appointmentList = existingAppointments.ToList();

            int maxConcurrent = 10;

            var allShifts = new (TimeOnly Start, TimeOnly End)[]
            {
                (new TimeOnly(7, 0),  new TimeOnly(11, 0)),
                (new TimeOnly(11, 0), new TimeOnly(14, 0)),
                (new TimeOnly(14, 0), new TimeOnly(18, 0)),
                (new TimeOnly(18, 0), new TimeOnly(22, 0)),
            };

            var slots = new List<AvailableSlotResponseDto>();

            foreach (var shift in allShifts)
            {
                int booked = appointmentList.Count(a =>
                    a.StartTime >= shift.Start && a.StartTime < shift.End);

                slots.Add(new AvailableSlotResponseDto
                {
                    StartTime = shift.Start,
                    EndTime = shift.End,
                    MaxCapacity = maxConcurrent,
                    BookedCount = booked,
                    RemainingCapacity = Math.Max(0, maxConcurrent - booked),
                    IsAvailable = booked < maxConcurrent
                });
            }

            return slots;
        }

        public async Task<Appointment> BookAsync(long customerId, BookAppointmentRequestDto dto, long? createdByStaffId = null)
        {
            if (dto.ScheduledDate < DateOnly.FromDateTime(DateTime.Today))
                throw new InvalidOperationException("Khong the dat lich cho ngay trong qua khu.");

            long? vehicleId = dto.VehicleId;

            if (vehicleId == null && !string.IsNullOrWhiteSpace(dto.LicensePlate) && dto.VehicleModelId.HasValue)
            {
                var plate = dto.LicensePlate.Trim().ToUpper();
                var existing = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.LicensePlate == plate);

                if (existing != null)
                {
                    vehicleId = existing.Id;
                }
                else
                {
                    var newVehicle = new Vehicle
                    {
                        LicensePlate = plate,
                        ModelId = dto.VehicleModelId.Value,
                        Color = dto.Color,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Vehicles.Add(newVehicle);
                    await _context.SaveChangesAsync();
                    vehicleId = newVehicle.Id;

                    _context.CustomerVehicles.Add(new CustomerVehicle
                    {
                        CustomerId = customerId,
                        VehicleId = newVehicle.Id,
                        IsCurrentOwner = true,
                        OwnershipStartDate = DateOnly.FromDateTime(DateTime.Today)
                    });
                    await _context.SaveChangesAsync();
                }
            }

            var endTime = dto.EndTime;

            if (!string.IsNullOrEmpty(dto.RequestedServiceIds))
            {
                var serviceIds = dto.RequestedServiceIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => long.TryParse(s.Trim(), out var v) ? v : 0)
                    .Where(v => v > 0)
                    .ToList();

                if (serviceIds.Count > 0)
                {
                    var totalMinutes = await _context.StandardServices
                        .Where(s => serviceIds.Contains(s.Id))
                        .SumAsync(s => s.StandardMinutes);

                    if (totalMinutes > 0)
                    {
                        var calculated = dto.StartTime.AddMinutes(totalMinutes);
                        if (calculated > endTime)
                            endTime = calculated;
                    }
                }
            }

            var appointment = new Appointment
            {
                BranchId = dto.BranchId,
                CustomerId = customerId,
                VehicleId = vehicleId,
                CreatedBy = createdByStaffId,
                ScheduledDate = dto.ScheduledDate,
                StartTime = dto.StartTime,
                EndTime = endTime,
                RequestedServiceIds = dto.RequestedServiceIds,
                CustomerNotes = dto.CustomerNotes,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };

            return await _appointmentRepository.AddAsync(appointment);
        }

        public async Task<Appointment> BookGuestAsync(BookAppointmentRequestDto dto)
        {
            if (dto.ScheduledDate < DateOnly.FromDateTime(DateTime.Today))
                throw new InvalidOperationException("Khong the dat lich cho ngay trong qua khu.");

            if (string.IsNullOrWhiteSpace(dto.GuestPhone))
                throw new InvalidOperationException("Vui long nhap so dien thoai.");

            var phone = dto.GuestPhone.Trim();
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == phone);

            if (customer == null)
            {
                customer = new Customer
                {
                    FullName = dto.GuestName?.Trim() ?? "Khach vang lai",
                    Phone = phone,
                    Email = dto.GuestEmail?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            return await BookAsync(customer.Id, dto);
        }

        public async Task<Appointment?> ConfirmAsync(long id)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(id);
            if (appointment == null) return null;

            if (appointment.Status != "PENDING")
                throw new InvalidOperationException($"Chỉ có thể xác nhận lịch hẹn đang ở trạng thái Chờ xác nhận. Trạng thái hiện tại: {appointment.Status}");

            appointment.Status = "CONFIRMED";
            appointment.UpdatedAt = DateTime.UtcNow;
            return await _appointmentRepository.UpdateAsync(id, appointment);
        }

        public async Task<Appointment?> CancelAsync(long id, string? reason)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(id);
            if (appointment == null) return null;

            if (appointment.Status != "PENDING" && appointment.Status != "CONFIRMED")
                throw new InvalidOperationException($"Khong the huy lich hen o trang thai: {appointment.Status}");

            appointment.Status = "CANCELED";
            appointment.CancelReason = reason;
            appointment.UpdatedAt = DateTime.UtcNow;
            return await _appointmentRepository.UpdateAsync(id, appointment);
        }

        public async Task<Appointment?> CheckInAsync(long id)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(id);
            if (appointment == null) return null;

            if (appointment.Status != "CONFIRMED")
                throw new InvalidOperationException($"Chỉ có thể check-in lịch hẹn đã được xác nhận. Trạng thái hiện tại: {appointment.Status}");

            appointment.Status = "ARRIVED";
            appointment.UpdatedAt = DateTime.UtcNow;
            return await _appointmentRepository.UpdateAsync(id, appointment);
        }

        public async Task<IEnumerable<AppointmentDetailResponseDto>> GetCustomerAppointmentsAsync(long customerId)
        {
            var appointments = await _appointmentRepository.GetByCustomerIdAsync(customerId);
            return await MapToDetailDtos(appointments);
        }

        public async Task<IEnumerable<AppointmentDetailResponseDto>> GetAllAppointmentsAsync(string? status = null)
        {
            var appointments = await _appointmentRepository.GetAllAsync(status);
            return await MapToDetailDtos(appointments);
        }

        public async Task<IEnumerable<AppointmentDetailResponseDto>> GetBranchAppointmentsAsync(long branchId, string? status = null)
        {
            var appointments = await _appointmentRepository.GetByBranchIdAsync(branchId, status);
            return await MapToDetailDtos(appointments);
        }

        public async Task<AppointmentDetailResponseDto?> GetDetailedAsync(long id)
        {
            var appointment = await _appointmentRepository.GetDetailedByIdAsync(id);
            if (appointment == null) return null;

            var dtos = await MapToDetailDtos(new[] { appointment });
            return dtos.FirstOrDefault();
        }

        private async Task<IEnumerable<AppointmentDetailResponseDto>> MapToDetailDtos(IEnumerable<Appointment> appointments)
        {
            var serviceCache = new Dictionary<long, string>();
            var result = new List<AppointmentDetailResponseDto>();

            foreach (var a in appointments)
            {
                var dto = new AppointmentDetailResponseDto
                {
                    Id = a.Id,
                    BranchId = a.BranchId,
                    BranchName = a.Branch?.Name ?? "",
                    CustomerId = a.CustomerId,
                    CustomerName = a.Customer?.FullName ?? "",
                    CustomerPhone = a.Customer?.Phone,
                    VehicleId = a.VehicleId,
                    VehicleLicensePlate = a.Vehicle?.LicensePlate,
                    VehicleModelName = a.Vehicle?.Model?.Brand?.Name != null && a.Vehicle?.Model?.ModelName != null
                        ? $"{a.Vehicle.Model.Brand.Name} {a.Vehicle.Model.ModelName}"
                        : null,
                    CreatedBy = a.CreatedBy,
                    ScheduledDate = a.ScheduledDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    CreatedAt = a.CreatedAt,
                    RequestedServiceIds = a.RequestedServiceIds,
                    CustomerNotes = a.CustomerNotes,
                    IsReminderSent = a.IsReminderSent,
                    Status = a.Status,
                    CancelReason = a.CancelReason,
                    UpdatedAt = a.UpdatedAt
                };

                if (!string.IsNullOrEmpty(a.RequestedServiceIds))
                {
                    var ids = a.RequestedServiceIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => long.TryParse(s.Trim(), out var v) ? v : 0)
                        .Where(v => v > 0)
                        .ToList();

                    foreach (var sid in ids)
                    {
                        if (!serviceCache.ContainsKey(sid))
                        {
                            var svc = await _context.StandardServices.FindAsync(sid);
                            serviceCache[sid] = svc?.Name ?? $"#{sid}";
                        }
                        dto.ServiceNames.Add(serviceCache[sid]);
                    }
                }

                result.Add(dto);
            }

            return result;
        }
    }
}
