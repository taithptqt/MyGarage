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

        private const int FastCapacity = 8;
        private const int RepairCapacity = 12;

        private static readonly TimeOnly OpenTime = new TimeOnly(8, 0);
        private static readonly TimeOnly CloseTime = new TimeOnly(20, 0);
        private static readonly TimeOnly FastLatestStart = new TimeOnly(18, 0);
        private static readonly TimeOnly RepairLatestStart = new TimeOnly(17, 0);

        private enum AppointmentMode
        {
            Fast,
            Repair
        }

        private static bool IsSunday(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday;

        private async Task<(AppointmentMode Mode, int TotalMinutes)> ResolveModeAndDurationAsync(string? requestedServiceIds)
        {
            if (string.IsNullOrWhiteSpace(requestedServiceIds))
                return (AppointmentMode.Fast, 0);

            var ids = requestedServiceIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), out var v) ? v : 0)
                .Where(v => v > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return (AppointmentMode.Fast, 0);

            var services = await _context.StandardServices
                .Include(s => s.Category)
                .Where(s => ids.Contains(s.Id) && s.Category != null)
                .ToListAsync();

            var mode = services.Any(s =>
                (s.Category.Code ?? "")
                .Contains("REPAIR", StringComparison.OrdinalIgnoreCase))
                ? AppointmentMode.Repair
                : AppointmentMode.Fast;

            var totalMinutes = services.Sum(s => s.StandardMinutes);
            return (mode, totalMinutes);
        }

        private async Task<Dictionary<long, AppointmentMode>> BuildServiceModeMapAsync(IEnumerable<long> serviceIds)
        {
            var ids = serviceIds?.ToList() ?? new List<long>();
            var result = new Dictionary<long, AppointmentMode>();
            if (ids.Count == 0) return result;

            var services = await _context.StandardServices
                .Include(s => s.Category)
                .Where(s => ids.Contains(s.Id) && s.Category != null)
                .ToListAsync();

            foreach (var s in services)
            {
                var mode = (s.Category.Code ?? "").Contains("REPAIR", StringComparison.OrdinalIgnoreCase)
                    ? AppointmentMode.Repair
                    : AppointmentMode.Fast;
                result[s.Id] = mode;
            }

            return result;
        }

        private AppointmentMode InferModeFromRequestedServiceIds(string? requestedServiceIds, Dictionary<long, AppointmentMode> serviceModeMap)
        {
            if (string.IsNullOrWhiteSpace(requestedServiceIds))
                return AppointmentMode.Fast;

            var ids = requestedServiceIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), out var v) ? v : 0)
                .Where(v => v > 0);

            // If any requested service has category code containing "REPAIR" => Repair
            return ids.Any(id => serviceModeMap.TryGetValue(id, out var m) && m == AppointmentMode.Repair)
                ? AppointmentMode.Repair
                : AppointmentMode.Fast;
        }

        public async Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(long branchId, DateOnly date, string? requestedServiceIds)
        {
            if (date < DateOnly.FromDateTime(DateTime.Today))
                return Enumerable.Empty<AvailableSlotResponseDto>();

            if (IsSunday(date))
                return Enumerable.Empty<AvailableSlotResponseDto>();

            var (mode, totalMinutes) = await ResolveModeAndDurationAsync(requestedServiceIds);
            var latestStart = mode == AppointmentMode.Fast ? FastLatestStart : RepairLatestStart;
            var capacity = mode == AppointmentMode.Fast ? FastCapacity : RepairCapacity;

            var existingAppointments = (await _appointmentRepository.GetByBranchAndDateAsync(branchId, date)).ToList();
            var activeAppointments = existingAppointments
                .Where(a => a.Status != "CANCELED" && a.Status != "NO_SHOW")
                .ToList();

            // Preload mode map for existing service ids to classify appointments
            var existingServiceIds = new HashSet<long>();
            foreach (var a in activeAppointments)
            {
                if (string.IsNullOrWhiteSpace(a.RequestedServiceIds)) continue;
                foreach (var id in a.RequestedServiceIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => long.TryParse(s.Trim(), out var v) ? v : 0)
                    .Where(v => v > 0))
                {
                    existingServiceIds.Add(id);
                }
            }

            var serviceModeMap = await BuildServiceModeMapAsync(existingServiceIds);

            var slots = new List<AvailableSlotResponseDto>();

            var candidateStart = OpenTime;
            while (candidateStart <= latestStart)
            {
                var candidateEnd = totalMinutes > 0 ? candidateStart.AddMinutes(totalMinutes) : candidateStart;
                if (candidateEnd > CloseTime)
                {
                    candidateStart = candidateStart.AddMinutes(15);
                    continue;
                }

                int overlapCount = 0;

                foreach (var a in activeAppointments)
                {
                    var apptMode = AppointmentMode.Fast;
                    if (!string.IsNullOrWhiteSpace(a.RequestedServiceIds))
                        apptMode = InferModeFromRequestedServiceIds(a.RequestedServiceIds, serviceModeMap);

                    if (apptMode != mode) continue;

                    // Overlap check: [candidateStart, candidateEnd) overlaps [a.StartTime, a.EndTime)
                    if (a.EndTime > candidateStart && a.StartTime < candidateEnd)
                        overlapCount++;
                }

                var remaining = Math.Max(0, capacity - overlapCount);

                slots.Add(new AvailableSlotResponseDto
                {
                    StartTime = candidateStart,
                    EndTime = candidateEnd,
                    MaxCapacity = capacity,
                    BookedCount = overlapCount,
                    RemainingCapacity = remaining,
                    IsAvailable = remaining > 0
                });

                candidateStart = candidateStart.AddMinutes(15);
            }

            return slots;
        }

        public async Task<Appointment> BookAsync(long customerId, BookAppointmentRequestDto dto, long? createdByStaffId = null)
        {
            if (dto.ScheduledDate < DateOnly.FromDateTime(DateTime.Today))
                throw new InvalidOperationException("Khong the dat lich cho ngay trong qua khu.");

            if (IsSunday(dto.ScheduledDate))
                throw new InvalidOperationException("Hom nay la chu nhat, khong the dat lich.");

            var (mode, totalMinutes) = await ResolveModeAndDurationAsync(dto.RequestedServiceIds);
            var latestStart = mode == AppointmentMode.Fast ? FastLatestStart : RepairLatestStart;
            var capacity = mode == AppointmentMode.Fast ? FastCapacity : RepairCapacity;

            if (dto.StartTime < OpenTime || dto.StartTime > latestStart)
                throw new InvalidOperationException("Thoi gian dat khong hop le theo loai dich vu.");

            var endTime = totalMinutes > 0 ? dto.StartTime.AddMinutes(totalMinutes) : dto.StartTime;
            if (endTime > CloseTime)
                throw new InvalidOperationException("Thoi gian ket thuc vuot qua gio hoat dong.");

            // Validate capacity by overlap on the selected mode
            var existingAppointments = (await _appointmentRepository.GetByBranchAndDateAsync(dto.BranchId, dto.ScheduledDate))
                .Where(a => a.Status != "CANCELED" && a.Status != "NO_SHOW")
                .ToList();

            var existingServiceIds = new HashSet<long>();
            foreach (var a in existingAppointments)
            {
                if (string.IsNullOrWhiteSpace(a.RequestedServiceIds)) continue;
                foreach (var id in a.RequestedServiceIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => long.TryParse(s.Trim(), out var v) ? v : 0)
                    .Where(v => v > 0))
                {
                    existingServiceIds.Add(id);
                }
            }

            var serviceModeMap = await BuildServiceModeMapAsync(existingServiceIds);

            int overlapCount = 0;
            foreach (var a in existingAppointments)
            {
                var apptMode = AppointmentMode.Fast;
                if (!string.IsNullOrWhiteSpace(a.RequestedServiceIds))
                    apptMode = InferModeFromRequestedServiceIds(a.RequestedServiceIds, serviceModeMap);

                if (apptMode != mode) continue;

                if (a.EndTime > dto.StartTime && a.StartTime < endTime)
                    overlapCount++;
            }

            if (overlapCount >= capacity)
                throw new InvalidOperationException("Ca nay da het cho hoac khong hop le.");

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

            // endTime is computed from requested services and validated above

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
