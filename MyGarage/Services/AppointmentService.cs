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

            // Fallback if not configured in DB
            if (opHour == null)
            {
                if (dayOfWeek == 0) return Enumerable.Empty<AvailableSlotResponseDto>(); // Sunday off
                
                opHour = new BranchOperatingHour
                {
                    OpenTime = new TimeOnly(7, 0),
                    CloseTime = new TimeOnly(17, 30),
                    MaxConcurrentBookings = 3,
                    SlotDurationMinutes = 60,
                    IsDayOff = false
                };
            }
            else if (opHour.IsDayOff == true)
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

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

            int slotMinutes = opHour.SlotDurationMinutes > 0 ? opHour.SlotDurationMinutes : 60;
            int maxConcurrent = opHour.MaxConcurrentBookings > 0 ? opHour.MaxConcurrentBookings : 3;

            var slots = new List<AvailableSlotResponseDto>();
            var current = openTime;

            while (current.AddMinutes(slotMinutes) <= closeTime)
            {
                var slotEnd = current.AddMinutes(slotMinutes);

                // Filter slots based on new rules: 07:00-11:30 and 13:30-17:00
                bool isMorning = current >= new TimeOnly(7, 0) && current <= new TimeOnly(11, 30);
                bool isAfternoon = current >= new TimeOnly(13, 30) && current <= new TimeOnly(17, 0);

                if (isMorning || isAfternoon)
                {
                    // Check if slot is in the past or too close to current time (30 mins buffer)
                    bool isReserved = false;
                    if (date == DateOnly.FromDateTime(DateTime.Today))
                    {
                        var now = TimeOnly.FromDateTime(DateTime.Now);
                        if (current < now.AddMinutes(30))
                        {
                            isReserved = true;
                        }
                    }

                    int booked = appointmentList.Count(a =>
                        TimeOnly.FromDateTime(a.StartTime) < slotEnd &&
                        TimeOnly.FromDateTime(a.EndTime) > current);

                    slots.Add(new AvailableSlotResponseDto
                    {
                        StartTime = date.ToDateTime(current),
                        EndTime = date.ToDateTime(slotEnd),
                        MaxCapacity = maxConcurrent,
                        BookedCount = booked,
                        RemainingCapacity = Math.Max(0, maxConcurrent - booked),
                        IsAvailable = !isReserved && booked < maxConcurrent
                    });
                }

                current = slotEnd;
            }

            return slots;
        }

        public async Task<Appointment> BookAsync(long authUserId, BookAppointmentRequestDto dto)
        {
            var requestedStartAt = dto.StartTime;
            var requestedEndAt = dto.EndTime;

            if (requestedStartAt == default || requestedEndAt == default)
                throw new InvalidOperationException("Thoi gian hen khong hop le.");

            if (requestedStartAt.Date < DateTime.Today)
                throw new InvalidOperationException("KhÃ´ng thá»ƒ Ä‘áº·t lá»‹ch cho ngÃ y trong quÃ¡ khá»©.");

            if (requestedStartAt.Date == DateTime.Today && requestedStartAt < DateTime.Now.AddMinutes(30))
                throw new InvalidOperationException("Thá»i gian háº¹n pháº£i cÃ¡ch thá»i gian hiá»‡n táº¡i Ã­t nháº¥t 30 phÃºt.");

            if (requestedEndAt <= requestedStartAt)
                throw new InvalidOperationException("Giá» káº¿t thÃºc pháº£i sau giá» báº¯t Ä‘áº§u.");

            long actualCustomerId = dto.CustomerId ?? authUserId;

            int dayOfWeek = (int)requestedStartAt.DayOfWeek;
            var opHour = await _context.BranchOperatingHours
                .FirstOrDefaultAsync(h => h.BranchId == dto.BranchId && h.DayOfWeek == dayOfWeek);

            if (opHour == null)
            {
                if (dayOfWeek == 0)
                    throw new InvalidOperationException("Gara khÃ´ng lÃ m viá»‡c vÃ o ngÃ y Chá»§ Nháº­t.");

                opHour = new BranchOperatingHour
                {
                    OpenTime = new TimeOnly(7, 0),
                    CloseTime = new TimeOnly(17, 30),
                    MaxConcurrentBookings = 3,
                    SlotDurationMinutes = 60,
                    IsDayOff = false
                };
            }
            else if (opHour.IsDayOff == true)
            {
                throw new InvalidOperationException("Gara khÃ´ng lÃ m viá»‡c vÃ o ngÃ y nÃ y.");
            }

            var requestedStartTimeOnly = TimeOnly.FromDateTime(requestedStartAt);
            bool isMorning = requestedStartTimeOnly >= new TimeOnly(7, 0) && requestedStartTimeOnly <= new TimeOnly(11, 30);
            bool isAfternoon = requestedStartTimeOnly >= new TimeOnly(13, 30) && requestedStartTimeOnly <= new TimeOnly(17, 0);
            if (!isMorning && !isAfternoon)
            {
                throw new InvalidOperationException("Giá» háº¹n pháº£i náº±m trong khoáº£ng 07:00 - 11:30 hoáº·c 13:30 - 17:00. Chá»‰ nháº­n xe trÆ°á»›c giá» nghá»‰/Ä‘Ã³ng cá»­a Ã­t nháº¥t 30 phÃºt.");
            }

            var dayStart = requestedStartAt.Date;
            var dayEnd = dayStart.AddDays(1);
            var existingAppointments = await _context.Appointments
                .Where(a => a.BranchId == dto.BranchId &&
                            a.StartTime >= dayStart &&
                            a.StartTime < dayEnd &&
                            a.Status != "CANCELLED" &&
                            a.Status != "CANCELED")
                .ToListAsync();

            int maxConcurrent = opHour.MaxConcurrentBookings > 0 ? opHour.MaxConcurrentBookings : 3;
            var overlapping = existingAppointments.Where(a =>
                a.StartTime < requestedEndAt && a.EndTime > requestedStartAt).ToList();

            var timePoints = overlapping.Select(a => a.StartTime)
                .Concat(overlapping.Select(a => a.EndTime))
                .Concat(new[] { requestedStartAt, requestedEndAt })
                .Where(t => t >= requestedStartAt && t <= requestedEndAt)
                .OrderBy(t => t)
                .Distinct()
                .ToList();

            for (int i = 0; i < timePoints.Count - 1; i++)
            {
                var intervalStart = timePoints[i];
                var intervalEnd = timePoints[i + 1];
                int count = overlapping.Count(a => a.StartTime < intervalEnd && a.EndTime > intervalStart);
                if (count >= maxConcurrent)
                {
                    throw new InvalidOperationException("Khung giá» nÃ y Ä‘Ã£ háº¿t chá»— hoáº·c vÆ°á»£t quÃ¡ cÃ´ng suáº¥t phá»¥c vá»¥ táº¡i thá»i Ä‘iá»ƒm Ä‘Ã³.");
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                long? vehicleId = dto.VehicleId;

                if (vehicleId == null && !string.IsNullOrWhiteSpace(dto.LicensePlate) && dto.VehicleModelId.HasValue)
                {
                    var plate = dto.LicensePlate.Trim().ToUpper();
                    var existing = await _context.Vehicles
                        .FirstOrDefaultAsync(v => v.LicensePlate == plate);

                    if (existing != null)
                    {
                        vehicleId = existing.Id;
                        if (existing.Vin == null && !string.IsNullOrWhiteSpace(dto.Vin))
                        {
                            existing.Vin = dto.Vin.Trim();
                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        var newVehicle = new Vehicle
                        {
                            LicensePlate = plate,
                            Vin = dto.Vin,
                            ModelId = dto.VehicleModelId.Value,
                            Color = dto.Color,
                            CreatedBy = dto.CreatedBy,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Vehicles.Add(newVehicle);
                        await _context.SaveChangesAsync();
                        vehicleId = newVehicle.Id;
                    }

                    var link = await _context.CustomerVehicles
                        .FirstOrDefaultAsync(cv => cv.CustomerId == actualCustomerId && cv.VehicleId == vehicleId && cv.IsDeleted != true);

                    if (link == null)
                    {
                        _context.CustomerVehicles.Add(new CustomerVehicle
                        {
                            CustomerId = actualCustomerId,
                            VehicleId = vehicleId.Value,
                            IsCurrentOwner = true,
                            OwnershipStartDate = DateOnly.FromDateTime(DateTime.Today),
                            CreatedBy = dto.CreatedBy,
                            CreatedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                    }
                    else if (link.CreatedBy == null)
                    {
                        link.CreatedBy = dto.CreatedBy;
                        await _context.SaveChangesAsync();
                    }
                }

                var appointment = new Appointment
                {
                    BranchId = dto.BranchId,
                    CustomerId = actualCustomerId,
                    VehicleId = vehicleId,
                    CreatedBy = dto.CreatedBy,
                    StartTime = requestedStartAt,
                    EndTime = requestedEndAt,
                    RequestedServiceIds = dto.RequestedServiceIds,
                    CustomerNotes = dto.CustomerNotes,
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return appointment;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
