using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public class AppointmentService : GenericService<Appointment>, IAppointmentService
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly GarageContext _context;
        private const int GraceArrivalMinutes = 15;
        private const int NoShowMinutes = 30;
        private const int LateCancelMinutes = 60;
        private const int BookingLockHours = 4;

        private static readonly HashSet<string> ClosedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "CANCELED", "CANCELLED", "NO_SHOW", "COMPLETED"
        };

        public AppointmentService(IAppointmentRepository repository, GarageContext context)
            : base(repository)
        {
            _appointmentRepository = repository;
            _context = context;
        }

        public async Task<IEnumerable<AvailableSlotResponseDto>> GetAvailableSlotsAsync(long branchId, DateOnly date, string? requestedServiceIds = null, string? bookingFlow = null)
        {
            if (date < DateOnly.FromDateTime(DateTime.Today))
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

            var flow = ResolveFlow(bookingFlow, requestedServiceIds);
            var dayOfWeek = (int)date.DayOfWeek;

            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == dayOfWeek);

            var openTime = opHour?.OpenTime ?? new TimeOnly(8, 0);
            var closeTime = opHour?.CloseTime ?? new TimeOnly(20, 0);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);
            if (opHour?.IsDayOff == true)
            {
                return Enumerable.Empty<AvailableSlotResponseDto>();
            }

            var exception = await _context.BranchExceptions
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.BranchId == branchId && e.ExceptionDate == date);

            if (exception?.IsClosed == true) return Enumerable.Empty<AvailableSlotResponseDto>();
            if (exception?.StartTime.HasValue == true) openTime = exception.StartTime.Value;
            if (exception?.EndTime.HasValue == true) closeTime = exception.EndTime.Value;

            if (flow == BookingFlowType.Flexible)
            {
                var windows = new List<AvailableSlotResponseDto>();
                var windowCursor = date.ToDateTime(openTime);
                var windowEndLimit = date.ToDateTime(closeTime);
                while (windowCursor.AddHours(1) <= windowEndLimit)
                {
                    windows.Add(new AvailableSlotResponseDto
                    {
                        StartTime = windowCursor,
                        EndTime = windowCursor.AddHours(1),
                        IsAvailable = true,
                        IsRoundedHour = windowCursor.Minute == 0,
                        IsTopPick = windowCursor.Minute == 0,
                        Session = ResolveSession(windowCursor.TimeOfDay),
                        CompactionScore = windowCursor.Minute == 0 ? 100 : 80,
                        BookingFlow = "FLEXIBLE"
                    });
                    windowCursor = windowCursor.AddMinutes(slotStepMinutes);
                }
                return windows;
            }

            var activeBays = await _context.Bays
                .AsNoTracking()
                .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true)
                .ToListAsync();
            var capacity = ComputeCapacity(activeBays);
            var onlineLimit = Math.Max(1, (int)Math.Floor(capacity.Total * 0.85m));
            var requestedCluster = await ResolveServiceClusterAsync(requestedServiceIds, null);
            var requiredDuration = await ResolveFixedDurationAsync(requestedServiceIds, slotStepMinutes);

            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            var appointments = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.BranchId == branchId && a.StartTime < dayEnd && a.EndTime > dayStart && !ClosedStatuses.Contains(a.Status))
                .ToListAsync();
            var overnightLocks = await _context.WorkSlots
                .AsNoTracking()
                .Where(s => s.BranchId == branchId
                    && s.SlotType == nameof(WorkSlotType.OVERNIGHT_LOCK)
                    && s.StartTime < dayEnd
                    && s.EndTime > dayStart
                    && s.Status != nameof(WorkSlotStatus.CANCELED)
                    && s.Status != "CANCELLED")
                .ToListAsync();

            var slots = new List<AvailableSlotResponseDto>();
            var current = date.ToDateTime(openTime);
            var lastStart = date.ToDateTime(closeTime).AddMinutes(-requiredDuration);
            while (current <= lastStart)
            {
                if (date == DateOnly.FromDateTime(DateTime.Today) && current < DateTime.Now.AddMinutes(30))
                {
                    current = current.AddMinutes(slotStepMinutes);
                    continue;
                }

                var end = current.AddMinutes(requiredDuration);
                var overlap = appointments.Where(a => a.StartTime < end && a.EndTime > current).ToList();
                var locked = CountLockedBays(activeBays, overnightLocks, current, end);
                var effectiveCapacity = new CapacityProfile(
                    Math.Max(0, capacity.Quick - locked.lockedQuick),
                    Math.Max(0, capacity.Repair - locked.lockedRepair),
                    Math.Max(0, capacity.Total - locked.lockedQuick - locked.lockedRepair));
                var effectiveOnlineLimit = Math.Max(1, (int)Math.Floor(effectiveCapacity.Total * 0.85m));
                if (effectiveCapacity.Total > 0 && IsSlotAvailable(overlap, requestedCluster, effectiveCapacity, effectiveOnlineLimit).isAvailable)
                {
                    var score = CalculateCompactionScore(current, date.ToDateTime(openTime), overlap);
                    slots.Add(new AvailableSlotResponseDto
                    {
                        StartTime = current,
                        EndTime = end,
                        MaxCapacity = effectiveOnlineLimit,
                        BookedCount = overlap.Count,
                        RemainingCapacity = Math.Max(0, effectiveOnlineLimit - overlap.Count),
                        IsAvailable = true,
                        IsRoundedHour = current.Minute == 0,
                        Session = ResolveSession(current.TimeOfDay),
                        CompactionScore = score,
                        BookingFlow = "FIXED"
                    });
                }

                current = current.AddMinutes(slotStepMinutes);
            }

            MarkTopPicks(slots);
            return slots.OrderBy(s => s.StartTime);
        }

        public async Task<Appointment> BookAsync(long authUserId, BookAppointmentRequestDto dto)
        {
            var flow = ResolveFlow(dto.BookingFlow, dto.RequestedServiceIds);
            long actualCustomerId = dto.CustomerId ?? authUserId;
            DateTime requestedStartAt;
            DateTime requestedEndAt;

            if (flow == BookingFlowType.Flexible)
            {
                requestedStartAt = dto.CheckInWindowStart ?? dto.StartTime
                    ?? throw new InvalidOperationException("Can cung cap khung gio check-in bat dau.");
                requestedEndAt = dto.CheckInWindowEnd ?? dto.EndTime
                    ?? throw new InvalidOperationException("Can cung cap khung gio check-in ket thuc.");
            }
            else
            {
                requestedStartAt = dto.StartTime
                    ?? throw new InvalidOperationException("Can cung cap gio bat dau cho lich co gio.");
                requestedEndAt = dto.EndTime ?? requestedStartAt;
            }

            if (requestedStartAt.Date < DateTime.Today)
                throw new InvalidOperationException("Khong the dat lich cho ngay trong qua khu.");

            if (requestedStartAt.Date == DateTime.Today && requestedStartAt < DateTime.Now.AddMinutes(30))
                throw new InvalidOperationException("Thoi gian hen phai cach hien tai it nhat 30 phut.");

            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == dto.BranchId && h.DayOfWeek == (int)requestedStartAt.DayOfWeek);
            if (opHour?.IsDayOff == true)
                throw new InvalidOperationException("Chi nhanh khong hoat dong vao ngay nay.");

            var openTime = opHour?.OpenTime ?? new TimeOnly(8, 0);
            var closeTime = opHour?.CloseTime ?? new TimeOnly(20, 0);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);

            if (flow == BookingFlowType.Fixed)
            {
                var fixedMinutes = await ResolveFixedDurationAsync(
                    dto.RequestedServiceIds,
                    slotStepMinutes,
                    dto.EndTime,
                    requestedStartAt);
                requestedEndAt = requestedStartAt.AddMinutes(fixedMinutes);
            }

            if (requestedEndAt <= requestedStartAt)
                throw new InvalidOperationException("Gio ket thuc phai sau gio bat dau.");

            ValidateTimeWindow(requestedStartAt, requestedEndAt, openTime, closeTime);

            var health = await GetBranchBookingHealthAsync(dto.BranchId, DateTime.Now);
            if (health.IsBookingLocked && health.LockUntil.HasValue && requestedStartAt < health.LockUntil.Value)
                throw new InvalidOperationException($"Chi nhanh dang tam khoa nhan lich den {health.LockUntil:HH:mm dd/MM/yyyy} do qua tai.");

            if (flow == BookingFlowType.Fixed)
            {
                var activeBays = await _context.Bays
                    .AsNoTracking()
                    .Where(b => b.BranchId == dto.BranchId && b.IsActive == true && b.IsDeleted != true)
                    .ToListAsync();
                var capacity = ComputeCapacity(activeBays);
                var requestedCluster = await ResolveServiceClusterAsync(dto.RequestedServiceIds, dto.CustomerNotes);

                var overlapping = await _context.Appointments
                    .AsNoTracking()
                    .Where(a => a.BranchId == dto.BranchId
                        && a.StartTime < requestedEndAt
                        && a.EndTime > requestedStartAt
                        && !ClosedStatuses.Contains(a.Status))
                    .ToListAsync();
                var overnightLocks = await _context.WorkSlots
                    .AsNoTracking()
                    .Where(s => s.BranchId == dto.BranchId
                        && s.SlotType == nameof(WorkSlotType.OVERNIGHT_LOCK)
                        && s.StartTime < requestedEndAt
                        && s.EndTime > requestedStartAt
                        && s.Status != nameof(WorkSlotStatus.CANCELED)
                        && s.Status != "CANCELLED")
                    .ToListAsync();
                var locked = CountLockedBays(activeBays, overnightLocks, requestedStartAt, requestedEndAt);
                var effectiveCapacity = new CapacityProfile(
                    Math.Max(0, capacity.Quick - locked.lockedQuick),
                    Math.Max(0, capacity.Repair - locked.lockedRepair),
                    Math.Max(0, capacity.Total - locked.lockedQuick - locked.lockedRepair));
                var effectiveOnlineLimit = Math.Max(1, (int)Math.Floor(effectiveCapacity.Total * 0.85m));

                if (effectiveCapacity.Total <= 0 || !IsSlotAvailable(overlapping, requestedCluster, effectiveCapacity, effectiveOnlineLimit).isAvailable)
                    throw new InvalidOperationException("Khung gio nay da het cong suat theo loai khoang hoac vuot nguong 85% online.");
            }
            else
            {
                var queueLoad = await _context.Appointments
                    .AsNoTracking()
                    .Where(a => a.BranchId == dto.BranchId
                        && a.StartTime < requestedEndAt
                        && a.EndTime > requestedStartAt
                        && (a.Status == "QUEUED" || a.Status == "ARRIVED" || a.Status == "ARRIVED_LATE"))
                    .CountAsync();

                if (queueLoad >= 40)
                    throw new InvalidOperationException("Khung check-in dang qua tai. Vui long chon khung khac.");
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
                    RequestedServiceIds = string.IsNullOrWhiteSpace(dto.RequestedServiceIds) && flow == BookingFlowType.Flexible
                        ? "0"
                        : dto.RequestedServiceIds,
                    CustomerNotes = AppendClusterMarker(AppendFlowMarker(dto.CustomerNotes, flow), flow == BookingFlowType.Flexible ? ServiceCluster.Heavy : await ResolveServiceClusterAsync(dto.RequestedServiceIds, dto.CustomerNotes)),
                    Status = flow == BookingFlowType.Flexible ? "QUEUED" : "PENDING",
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

            if (!string.Equals(appointment.Status, "PENDING", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, "QUEUED", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Chi co the xac nhan lich hen cho trang thai PENDING/QUEUED. Hien tai: {appointment.Status}");

            appointment.Status = "CONFIRMED";
            appointment.UpdatedAt = DateTime.UtcNow;
            return await _appointmentRepository.UpdateAsync(id, appointment);
        }

        public async Task<Appointment?> CancelAsync(long id, string? reason)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(id);
            if (appointment == null) return null;

            if (ClosedStatuses.Contains(appointment.Status))
                throw new InvalidOperationException($"Khong the huy lich hen o trang thai: {appointment.Status}");

            var effectiveReason = reason;
            if (appointment.StartTime > DateTime.Now && (appointment.StartTime - DateTime.Now).TotalMinutes < LateCancelMinutes)
            {
                effectiveReason = string.IsNullOrWhiteSpace(reason) ? "[LATE_CANCEL]" : $"[LATE_CANCEL] {reason}";
            }

            appointment.Status = "CANCELED";
            appointment.CancelReason = effectiveReason;
            appointment.UpdatedAt = DateTime.UtcNow;
            return await _appointmentRepository.UpdateAsync(id, appointment);
        }

        public async Task<Appointment?> CheckInAsync(long id)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(id);
            if (appointment == null) return null;
            if (!string.Equals(appointment.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(appointment.Status, "QUEUED", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Chi co the check-in lich da xac nhan/queued. Hien tai: {appointment.Status}");

            appointment.Status = "ARRIVED";
            appointment.UpdatedAt = DateTime.UtcNow;
            return await _appointmentRepository.UpdateAsync(id, appointment);
        }

        public async Task<Appointment?> ArriveAsync(long id, DateTime? arrivedAt = null)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(id);
            if (appointment == null) return null;
            if (ClosedStatuses.Contains(appointment.Status))
                throw new InvalidOperationException($"Khong the tiep nhan lich o trang thai: {appointment.Status}");

            var arriveTime = arrivedAt ?? DateTime.Now;
            var flow = ResolveFlowByAppointment(appointment);

            if (flow == BookingFlowType.Flexible)
            {
                if (arriveTime > appointment.EndTime.AddMinutes(NoShowMinutes))
                {
                    appointment.Status = "QUEUED";
                    appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "WINDOW_MISSED_REPRIORITIZED");
                }
                else
                {
                    appointment.Status = "ARRIVED";
                }

                appointment.UpdatedAt = DateTime.UtcNow;
                return await _appointmentRepository.UpdateAsync(id, appointment);
            }

            if (arriveTime > appointment.StartTime.AddMinutes(NoShowMinutes))
            {
                appointment.Status = "NO_SHOW";
                appointment.CancelReason = "AUTO_NO_SHOW_LATE_ARRIVAL";
                appointment.UpdatedAt = DateTime.UtcNow;
                return await _appointmentRepository.UpdateAsync(id, appointment);
            }

            if (arriveTime > appointment.StartTime.AddMinutes(GraceArrivalMinutes))
            {
                var lateMinutes = (arriveTime - appointment.StartTime).TotalMinutes;
                if (lateMinutes <= NoShowMinutes)
                {
                    var candidates = (await GetAvailableSlotsAsync(appointment.BranchId, DateOnly.FromDateTime(appointment.StartTime), appointment.RequestedServiceIds, "FIXED"))
                        .Where(s => s.StartTime >= arriveTime.AddMinutes(15))
                        .OrderByDescending(s => s.IsTopPick)
                        .ThenByDescending(s => s.CompactionScore)
                        .ThenBy(s => s.StartTime)
                        .ToList();

                    if (candidates.Count > 0)
                    {
                        var next = candidates[0];
                        appointment.StartTime = next.StartTime;
                        appointment.EndTime = next.EndTime;
                        appointment.Status = "ARRIVED_LATE";
                        appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "LATE_RESCHEDULED");
                    }
                    else
                    {
                        appointment.Status = "QUEUED";
                        appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "LATE_SWITCHED_TO_FLEX_QUEUE");
                    }
                }
                else
                {
                    appointment.Status = "QUEUED";
                    appointment.CustomerNotes = AppendNoteMarker(appointment.CustomerNotes, "LATE_SWITCHED_TO_FLEX_QUEUE");
                }
                appointment.UpdatedAt = DateTime.UtcNow;
                return await _appointmentRepository.UpdateAsync(id, appointment);
            }

            appointment.Status = "ARRIVED";
            appointment.UpdatedAt = DateTime.UtcNow;
            return await _appointmentRepository.UpdateAsync(id, appointment);
        }

        public async Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null)
        {
            var at = now ?? DateTime.Now;
            var candidates = await _context.Appointments
                .Where(a => a.BranchId == branchId
                    && (a.Status == "CONFIRMED" || a.Status == "PENDING")
                    && a.StartTime <= at.AddMinutes(-NoShowMinutes))
                .ToListAsync();

            foreach (var a in candidates)
            {
                a.Status = "NO_SHOW";
                a.CancelReason = "AUTO_NO_SHOW_TIMEOUT";
                a.UpdatedAt = DateTime.UtcNow;
            }

            if (candidates.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return candidates.Count;
        }

        public async Task<BranchBookingHealthResponseDto> GetBranchBookingHealthAsync(long branchId, DateTime? at = null)
        {
            var atTime = at ?? DateTime.Now;
            var bays = await _context.Bays.AsNoTracking().Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true).ToListAsync();
            var capacity = ComputeCapacity(bays);
            var delayThreshold = Math.Max(120, capacity.Total * 15);
            var parkingThreshold = Math.Max(12, (int)Math.Ceiling(capacity.Total * 1.5));

            var delayedSlots = await _context.WorkSlots.AsNoTracking()
                .Where(s => s.BranchId == branchId
                    && s.EndTime < atTime
                    && s.Status != nameof(WorkSlotStatus.COMPLETED)
                    && s.Status != nameof(WorkSlotStatus.CANCELED)
                    && s.Status != "CANCELLED")
                .ToListAsync();

            var totalDelay = delayedSlots.Sum(s => (int)Math.Max(0, Math.Ceiling(((s.ActualReleaseTime ?? atTime) - s.EndTime).TotalMinutes)));
            var queueCount = await _context.Appointments.AsNoTracking()
                .Where(a => a.BranchId == branchId && (a.Status == "QUEUED" || a.Status == "ARRIVED" || a.Status == "ARRIVED_LATE"))
                .CountAsync();

            var locked = totalDelay >= delayThreshold || (totalDelay >= delayThreshold / 2 && queueCount >= parkingThreshold);
            return new BranchBookingHealthResponseDto
            {
                BranchId = branchId,
                EvaluatedAt = atTime,
                IsBookingLocked = locked,
                LockUntil = locked ? atTime.AddHours(BookingLockHours) : null,
                TotalDelayMinutes = totalDelay,
                DelayThresholdMinutes = delayThreshold,
                ParkingQueueCount = queueCount,
                ParkingThreshold = parkingThreshold,
                Message = locked ? "Chi nhanh dang qua tai, tam khoa nhan lich 4 gio." : "Chi nhanh dang trong nguong an toan.",
                SuggestedBranches = locked ? await GetAlternativeBranchesAsync(branchId, atTime) : new List<BranchSuggestionDto>()
            };
        }

        public async Task<DispatchDashboardResponseDto> GetDispatchDashboardAsync(long branchId, DateTime? at = null)
        {
            var snapshot = at ?? DateTime.Now;
            var bays = await _context.Bays.AsNoTracking().Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true).OrderBy(b => b.Code).ToListAsync();
            var slots = await _context.WorkSlots.AsNoTracking()
                .Where(s => s.BranchId == branchId
                    && s.StartTime <= snapshot
                    && (s.ActualReleaseTime == null || s.ActualReleaseTime > snapshot)
                    && s.Status != nameof(WorkSlotStatus.CANCELED)
                    && s.Status != "CANCELLED")
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();

            var bayDtos = new List<DispatchBayStatusDto>();
            var busy = 0;
            var overflow = 0;
            foreach (var bay in bays)
            {
                var slot = slots.FirstOrDefault(s => s.BayId == bay.Id);
                if (slot != null) busy++;
                var isOverflow = slot != null && !IsQuickBay(bay.BayType) && IsQuickSlotType(slot.SlotType, slot.Notes);
                if (isOverflow) overflow++;

                bayDtos.Add(new DispatchBayStatusDto
                {
                    BayId = bay.Id,
                    BayCode = bay.Code,
                    BayName = bay.Name,
                    BayType = bay.BayType,
                    Status = slot == null ? "IDLE" : "BUSY",
                    IsOverflowFromQuick = isOverflow,
                    SlotType = slot?.SlotType,
                    CurrentCustomerId = slot?.CustomerId,
                    CurrentVehicleId = slot?.VehicleId,
                    PlannedReleaseAt = slot?.EndTime,
                    ActualReleaseAt = slot?.ActualReleaseTime
                });
            }

            var queue = await _context.Appointments.AsNoTracking().Where(a => a.BranchId == branchId && (a.Status == "QUEUED" || a.Status == "ARRIVED" || a.Status == "ARRIVED_LATE")).CountAsync();
            return new DispatchDashboardResponseDto
            {
                BranchId = branchId,
                SnapshotAt = snapshot,
                TotalBays = bays.Count,
                BusyBays = busy,
                IdleBays = bays.Count - busy,
                OverflowBays = overflow,
                WaitingQueueCount = queue,
                Bays = bayDtos
            };
        }

        public async Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null)
        {
            var executedAt = at ?? DateTime.Now;
            var noShows = await AutoMarkNoShowsAsync(branchId, executedAt);
            var health = await GetBranchBookingHealthAsync(branchId, executedAt);
            var delayNotifications = 0;

            if (health.IsBookingLocked)
            {
                delayNotifications = await SendDelayNotificationsAsync(branchId, executedAt, health);
            }

            return new DispatchCycleResponseDto
            {
                BranchId = branchId,
                ExecutedAt = executedAt,
                AutoNoShowCount = noShows,
                DelayNotificationCount = delayNotifications,
                BookingHealth = health
            };
        }

        public async Task<OvernightLockResponseDto> LockOvernightSlotsAsync(long branchId, DateOnly? businessDate = null)
        {
            var date = businessDate ?? DateOnly.FromDateTime(DateTime.Today);
            var closingTime = date.ToDateTime(new TimeOnly(18, 0));
            var nextMorning = date.AddDays(1).ToDateTime(new TimeOnly(8, 0));
            var opHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == (int)date.DayOfWeek);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);

            var unfinished = await _context.WorkSlots
                .Where(s => s.BranchId == branchId
                    && s.StartTime <= closingTime
                    && s.SlotType != nameof(WorkSlotType.OVERNIGHT_LOCK)
                    && s.Status != nameof(WorkSlotStatus.COMPLETED)
                    && s.Status != nameof(WorkSlotStatus.CANCELED)
                    && s.Status != "CANCELLED"
                    && (s.ActualReleaseTime == null || s.ActualReleaseTime > closingTime))
                .ToListAsync();

            var result = new OvernightLockResponseDto
            {
                BranchId = branchId,
                BusinessDate = date
            };

            foreach (var source in unfinished)
            {
                var marker = $"[OVERNIGHT_FROM:{source.Id}]";
                var exists = await _context.WorkSlots.AnyAsync(s =>
                    s.BranchId == branchId
                    && s.BayId == source.BayId
                    && s.SlotType == nameof(WorkSlotType.OVERNIGHT_LOCK)
                    && s.StartTime >= nextMorning
                    && s.StartTime < nextMorning.AddDays(1)
                    && s.Notes != null
                    && s.Notes.Contains(marker));

                if (exists)
                {
                    continue;
                }

                var baseRemaining = source.ActualReleaseTime.HasValue && source.ActualReleaseTime.Value > closingTime
                    ? (int)Math.Ceiling((source.ActualReleaseTime.Value - closingTime).TotalMinutes)
                    : 90;
                var lockMinutes = RoundUpToStep(Math.Clamp(baseRemaining, 60, 240), slotStepMinutes);

                var lockSlot = new WorkSlot
                {
                    ParentSlotId = source.Id,
                    BranchId = source.BranchId,
                    BayId = source.BayId,
                    TechnicianId = source.TechnicianId,
                    StartTime = nextMorning,
                    EndTime = nextMorning.AddMinutes(lockMinutes),
                    SlotType = nameof(WorkSlotType.OVERNIGHT_LOCK),
                    CustomerId = source.CustomerId,
                    VehicleId = source.VehicleId,
                    Status = nameof(WorkSlotStatus.LOCKED),
                    Notes = marker
                };

                _context.WorkSlots.Add(lockSlot);
                await _context.SaveChangesAsync();

                result.LockedSlots.Add(new OvernightLockedSlotDto
                {
                    SourceWorkSlotId = source.Id,
                    LockedWorkSlotId = lockSlot.Id,
                    BayId = source.BayId,
                    LockedFrom = lockSlot.StartTime,
                    LockedTo = lockSlot.EndTime,
                    VehicleId = source.VehicleId
                });
            }

            result.LockedSlotCount = result.LockedSlots.Count;
            return result;
        }

        private async Task<int> SendDelayNotificationsAsync(long branchId, DateTime now, BranchBookingHealthResponseDto health)
        {
            if (!health.IsBookingLocked || !health.LockUntil.HasValue)
            {
                return 0;
            }

            var affected = await _context.Appointments
                .Where(a => a.BranchId == branchId
                    && (a.Status == "CONFIRMED" || a.Status == "PENDING")
                    && a.StartTime >= now.AddMinutes(90)
                    && a.StartTime <= health.LockUntil.Value)
                .ToListAsync();

            var suggestion = health.SuggestedBranches.FirstOrDefault();
            foreach (var appointment in affected)
            {
                var suffix = suggestion == null
                    ? "Chi nhanh dang qua tai, vui long den som de duoc ho tro."
                    : $"Ban co the tham khao {suggestion.BranchName} (~{suggestion.DistanceKm} km) dang trong cho.";

                _context.Notifications.Add(new Notification
                {
                    CustomerId = appointment.CustomerId,
                    Title = "Lich hen co nguy co tre",
                    Content = $"Lich hen {appointment.StartTime:HH:mm dd/MM} co the bi tre do xuong qua tai. {suffix}",
                    IsRead = false,
                    SentAt = DateTime.UtcNow,
                    LinkAction = $"/appointments/{appointment.Id}"
                });
            }

            if (affected.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return affected.Count;
        }

        private static (int lockedQuick, int lockedRepair) CountLockedBays(List<Bay> activeBays, List<WorkSlot> locks, DateTime start, DateTime end)
        {
            var bayById = activeBays.ToDictionary(b => b.Id, b => b);
            var overlapping = locks.Where(l => l.StartTime < end && l.EndTime > start).ToList();
            var quick = 0;
            var repair = 0;

            foreach (var lockSlot in overlapping)
            {
                if (!bayById.TryGetValue(lockSlot.BayId, out var bay))
                {
                    continue;
                }

                if (IsQuickBay(bay.BayType))
                {
                    quick++;
                }
                else
                {
                    repair++;
                }
            }

            return (quick, repair);
        }


        private static BookingFlowType ResolveFlow(string? bookingFlow, string? requestedServiceIds)
        {
            if (!string.IsNullOrWhiteSpace(bookingFlow))
            {
                var key = bookingFlow.Trim().ToLowerInvariant();
                if (key.Contains("flex") || key.Contains("window") || key.Contains("check")) return BookingFlowType.Flexible;
            }

            return string.IsNullOrWhiteSpace(requestedServiceIds) ? BookingFlowType.Flexible : BookingFlowType.Fixed;
        }

        private static BookingFlowType ResolveFlowByAppointment(Appointment appointment)
        {
            if (!string.IsNullOrWhiteSpace(appointment.CustomerNotes) && appointment.CustomerNotes.Contains("[FLOW:FLEXIBLE]", StringComparison.OrdinalIgnoreCase))
                return BookingFlowType.Flexible;
            return ResolveFlow(null, appointment.RequestedServiceIds);
        }

        private static string ResolveSession(TimeSpan time)
        {
            if (time < new TimeSpan(12, 0, 0)) return "MORNING";
            if (time < new TimeSpan(17, 0, 0)) return "AFTERNOON";
            return "EVENING";
        }

        private static void MarkTopPicks(List<AvailableSlotResponseDto> slots)
        {
            foreach (var sessionGroup in slots.GroupBy(s => s.Session))
            {
                var rounded = sessionGroup.Where(s => s.IsRoundedHour).OrderByDescending(s => s.CompactionScore).ThenBy(s => s.StartTime).Take(5).ToList();
                var picks = new List<AvailableSlotResponseDto>(rounded);
                if (picks.Count < 3)
                {
                    picks.AddRange(sessionGroup.Where(s => !picks.Contains(s)).OrderByDescending(s => s.CompactionScore).ThenBy(s => s.StartTime).Take(3 - picks.Count));
                }

                foreach (var slot in picks) slot.IsTopPick = true;
            }
        }

        private static int CalculateCompactionScore(DateTime slotStart, DateTime openAt, List<Appointment> overlap)
        {
            var score = slotStart.Minute == 0 ? 30 : 0;
            if (slotStart == openAt) score += 20;
            var nearestEnd = overlap.Select(o => Math.Abs((o.EndTime - slotStart).TotalMinutes)).DefaultIfEmpty(999).Min();
            if (nearestEnd <= 15) score += 40;
            else if (nearestEnd <= 30) score += 20;
            return score;
        }

        private async Task<int> ResolveFixedDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            DateTime? requestedEnd = null,
            DateTime? requestedStart = null)
        {
            if (requestedStart.HasValue && requestedEnd.HasValue && requestedEnd > requestedStart)
            {
                return RoundUpToStep((int)Math.Ceiling((requestedEnd.Value - requestedStart.Value).TotalMinutes), slotStepMinutes);
            }

            var serviceIds = ParseServiceIds(requestedServiceIds).ToList();
            if (!serviceIds.Any()) return RoundUpToStep(60 + slotStepMinutes, slotStepMinutes);
            var minutes = await _context.StandardServices.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).SumAsync(s => (int?)s.StandardMinutes) ?? 60;
            return RoundUpToStep(minutes + slotStepMinutes, slotStepMinutes);
        }

        private async Task<ServiceCluster> ResolveServiceClusterAsync(string? requestedServiceIds, string? notes)
        {
            var ids = ParseServiceIds(requestedServiceIds).ToList();
            if (!ids.Any()) return IsHeavyHint(notes) ? ServiceCluster.Heavy : ServiceCluster.Quick;

            var services = await _context.StandardServices.AsNoTracking().Include(s => s.Category).Where(s => ids.Contains(s.Id)).ToListAsync();
            var heavy = services.Any(s => IsHeavyHint(s.Name) || IsHeavyHint(s.Category?.Name));
            return heavy ? ServiceCluster.Heavy : ServiceCluster.Quick;
        }

        private static (bool isAvailable, int bookedQuick, int bookedHeavy) IsSlotAvailable(List<Appointment> overlap, ServiceCluster requestedCluster, CapacityProfile capacity, int onlineLimit)
        {
            if (overlap.Count >= onlineLimit) return (false, 0, 0);
            int quickBooked = overlap.Count(a => ResolveFlowByRequestedServices(a.RequestedServiceIds, a.CustomerNotes) == ServiceCluster.Quick);
            int heavyBooked = overlap.Count - quickBooked;

            if (requestedCluster == ServiceCluster.Heavy)
            {
                return (heavyBooked < capacity.Repair, quickBooked, heavyBooked);
            }

            int quickCapacityWithOverflow = capacity.Quick + Math.Max(0, capacity.Repair - heavyBooked);
            return (quickBooked < quickCapacityWithOverflow, quickBooked, heavyBooked);
        }

        private static ServiceCluster ResolveFlowByRequestedServices(string? requestedServiceIds, string? notes)
        {
            if (!string.IsNullOrWhiteSpace(notes) && notes.Contains("[CLUSTER:HEAVY]", StringComparison.OrdinalIgnoreCase))
                return ServiceCluster.Heavy;
            if (!string.IsNullOrWhiteSpace(notes) && notes.Contains("[CLUSTER:QUICK]", StringComparison.OrdinalIgnoreCase))
                return ServiceCluster.Quick;
            if (IsHeavyHint(notes)) return ServiceCluster.Heavy;
            return string.IsNullOrWhiteSpace(requestedServiceIds) ? ServiceCluster.Quick : ServiceCluster.Quick;
        }

        private static bool IsHeavyHint(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var text = NormalizeText(value);
            return text.Contains("sua chua") || text.Contains("repair") || text.Contains("bao hanh") || text.Contains("bao hiem") || text.Contains("rework") || text.Contains("chan doan") || text.Contains("kiem tra");
        }

        private static string NormalizeText(string value)
        {
            var lowered = value.Trim().ToLowerInvariant();
            return string.Concat(lowered.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC);
        }

        private static CapacityProfile ComputeCapacity(List<Bay> activeBays)
        {
            if (activeBays.Count == 0) return new CapacityProfile(8, 12, 20);
            int quick = activeBays.Count(b => IsQuickBay(b.BayType));
            int repair = activeBays.Count - quick;
            if (quick == 0 && repair > 0)
            {
                quick = Math.Min(8, repair);
                repair -= quick;
            }
            return new CapacityProfile(quick, repair, quick + repair);
        }

        private static bool IsQuickBay(string? bayType)
        {
            if (string.IsNullOrWhiteSpace(bayType)) return false;
            var key = NormalizeText(bayType);
            return key.Contains("quick") || key.Contains("fast") || key.Contains("bao duong") || key.Contains("dich vu nhanh");
        }

        private static bool IsQuickSlotType(string? slotType, string? notes)
        {
            var key = NormalizeText($"{slotType} {notes}");
            return key.Contains("quick") || key.Contains("maintenance") || key.Contains("bao duong") || key.Contains("dich vu nhanh");
        }

        private static int RoundUpToStep(int minutes, int stepMinutes)
        {
            var step = stepMinutes > 0 ? stepMinutes : 15;
            if (minutes <= 0) return step;
            var mod = minutes % step;
            return mod == 0 ? minutes : minutes + (step - mod);
        }

        private static int ResolveSlotStepMinutes(int? slotDurationMinutes)
        {
            var step = slotDurationMinutes.GetValueOrDefault(15);
            return step > 0 ? step : 15;
        }

        private static void ValidateTimeWindow(DateTime startAt, DateTime endAt, TimeOnly openTime, TimeOnly closeTime)
        {
            if (endAt <= startAt) throw new InvalidOperationException("Gio ket thuc phai sau gio bat dau.");
            if (startAt.Date != endAt.Date) throw new InvalidOperationException("Lich hen phai nam trong cung mot ngay.");
            var start = TimeOnly.FromDateTime(startAt);
            var end = TimeOnly.FromDateTime(endAt);
            if (start < openTime || end > closeTime) throw new InvalidOperationException($"Lich hen phai trong gio lam viec {openTime:HH\\:mm}-{closeTime:HH\\:mm}.");
        }

        private static string AppendFlowMarker(string? notes, BookingFlowType flow)
        {
            var marker = flow == BookingFlowType.Flexible ? "[FLOW:FLEXIBLE]" : "[FLOW:FIXED]";
            if (string.IsNullOrWhiteSpace(notes)) return marker;
            if (notes.Contains("[FLOW:", StringComparison.OrdinalIgnoreCase)) return notes;
            return $"{notes} {marker}";
        }

        private static string AppendClusterMarker(string? notes, ServiceCluster cluster)
        {
            var marker = cluster == ServiceCluster.Heavy ? "[CLUSTER:HEAVY]" : "[CLUSTER:QUICK]";
            if (string.IsNullOrWhiteSpace(notes)) return marker;
            if (notes.Contains("[CLUSTER:", StringComparison.OrdinalIgnoreCase)) return notes;
            return $"{notes} {marker}";
        }

        private static string AppendNoteMarker(string? notes, string marker)
        {
            var tag = $"[{marker}]";
            if (string.IsNullOrWhiteSpace(notes)) return tag;
            if (notes.Contains(tag, StringComparison.OrdinalIgnoreCase)) return notes;
            return $"{notes} {tag}";
        }

        private static IEnumerable<long> ParseServiceIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Enumerable.Empty<long>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
                .Where(id => id > 0);
        }

        private async Task<List<BranchSuggestionDto>> GetAlternativeBranchesAsync(long currentBranchId, DateTime at)
        {
            var current = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == currentBranchId && b.IsDeleted != true);
            var branches = await _context.Branches.AsNoTracking().Where(b => b.Id != currentBranchId && b.IsDeleted != true).ToListAsync();
            var output = new List<BranchSuggestionDto>();

            foreach (var branch in branches)
            {
                var bayCount = await _context.Bays.AsNoTracking()
                    .Where(b => b.BranchId == branch.Id && b.IsActive == true && b.IsDeleted != true)
                    .CountAsync();
                if (bayCount <= 0) bayCount = 20;

                var active = await _context.Appointments.AsNoTracking()
                    .Where(a => a.BranchId == branch.Id && a.StartTime < at.AddMinutes(15) && a.EndTime > at && !ClosedStatuses.Contains(a.Status))
                    .CountAsync();
                var utilization = Math.Min(100d, active * 100d / bayCount);
                if (utilization >= 85d) continue;

                output.Add(new BranchSuggestionDto
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    DistanceKm = HaversineDistanceKm(current?.Lat, current?.Lng, branch.Lat, branch.Lng),
                    EstimatedUtilizationPct = Math.Round(utilization, 1),
                    EarliestAvailableAt = at.AddMinutes(30)
                });
            }

            return output.OrderBy(b => b.DistanceKm).ThenBy(b => b.EstimatedUtilizationPct).Take(3).ToList();
        }

        private static double HaversineDistanceKm(decimal? lat1, decimal? lon1, decimal? lat2, decimal? lon2)
        {
            if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue) return 9999;
            const double r = 6371d;
            var dLat = ToRadians((double)(lat2.Value - lat1.Value));
            var dLon = ToRadians((double)(lon2.Value - lon1.Value));
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(ToRadians((double)lat1.Value)) * Math.Cos(ToRadians((double)lat2.Value))
                    * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(r * c, 1);
        }

        private static double ToRadians(double v) => v * Math.PI / 180d;

        private sealed record CapacityProfile(int Quick, int Repair, int Total);

        private enum BookingFlowType
        {
            Fixed,
            Flexible
        }

        private enum ServiceCluster
        {
            Quick,
            Heavy
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
