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
                        a.StartTime < slotEnd && a.EndTime > current);

                    slots.Add(new AvailableSlotResponseDto
                    {
                        StartTime = current,
                        EndTime = slotEnd,
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
            if (dto.ScheduledDate < DateOnly.FromDateTime(DateTime.Today))
                throw new InvalidOperationException("Không thể đặt lịch cho ngày trong quá khứ.");

            if (dto.ScheduledDate == DateOnly.FromDateTime(DateTime.Today))
            {
                var now = TimeOnly.FromDateTime(DateTime.Now);
                if (dto.StartTime < now.AddMinutes(30))
                    throw new InvalidOperationException("Thời gian hẹn phải cách thời gian hiện tại ít nhất 30 phút.");
            }

            // 1. Determine actual customerId (If staff books, they might provide a CustomerId in the DTO)
            long actualCustomerId = dto.CustomerId ?? authUserId;

            // 2. Validate Operating Hours
            int dayOfWeek = (int)dto.ScheduledDate.DayOfWeek;
            var opHour = await _context.BranchOperatingHours
                .FirstOrDefaultAsync(h => h.BranchId == dto.BranchId && h.DayOfWeek == dayOfWeek);

            // Use fallback if not configured in DB (Assume open Mon-Sat, Sunday off)
            if (opHour == null)
            {
                if (dayOfWeek == 0) // Sunday
                    throw new InvalidOperationException("Gara không làm việc vào ngày Chủ Nhật.");
                
                // Default settings if missing in DB
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
                throw new InvalidOperationException("Gara không làm việc vào ngày này.");
            }

            // Check against Morning and Afternoon intervals
            bool isMorning = dto.StartTime >= new TimeOnly(7, 0) && dto.StartTime <= new TimeOnly(11, 30);
            bool isAfternoon = dto.StartTime >= new TimeOnly(13, 30) && dto.StartTime <= new TimeOnly(17, 0);

            if (!isMorning && !isAfternoon)
            {
                throw new InvalidOperationException("Giờ hẹn phải nằm trong khoảng 07:00 - 11:30 hoặc 13:30 - 17:00. Chỉ nhận xe trước giờ nghỉ/đóng cửa ít nhất 30 phút.");
            }

            // 3. Check Capacity (Flexible range check)
            var existingAppointments = await _context.Appointments
                .Where(a => a.BranchId == dto.BranchId && a.ScheduledDate == dto.ScheduledDate && a.Status != "CANCELLED")
                .ToListAsync();

            int maxConcurrent = opHour.MaxConcurrentBookings > 0 ? opHour.MaxConcurrentBookings : 3;
            
            // Check if any sub-interval within [StartTime, EndTime] exceeds capacity
            var overlapping = existingAppointments.Where(a => 
                a.StartTime < dto.EndTime && a.EndTime > dto.StartTime).ToList();

            // Collect critical time points to check concurrency levels
            var timePoints = overlapping.Select(a => a.StartTime)
                .Concat(overlapping.Select(a => a.EndTime))
                .Concat(new[] { dto.StartTime, dto.EndTime })
                .Where(t => t >= dto.StartTime && t <= dto.EndTime)
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
                    throw new InvalidOperationException("Khung giờ này đã hết chỗ hoặc vượt quá công suất phục vụ tại thời điểm đó.");
                }
            }

            // Start Atomic Transaction
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

                // Ensure Customer-Vehicle association exists
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
                    ScheduledDate = dto.ScheduledDate,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime,
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
