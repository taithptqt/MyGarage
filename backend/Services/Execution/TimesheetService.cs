using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Exceptions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class TimesheetService : ITimesheetService
    {
        private const double MaxCheckInDistanceMeters = 150d;
        private const int OnTimeToleranceMinutes = 5;
        private readonly IGarageContext _context;

        public TimesheetService(IGarageContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<TimesheetEntryDto>> GetTodayEntriesAsync(long staffId)
        {
            if (staffId <= 0)
            {
                throw new BadRequestException("Invalid staff id.");
            }

            var staffExists = await _context.Staffs.AsNoTracking()
                .AnyAsync(x => x.Id == staffId && x.IsDeleted != true);
            if (!staffExists)
            {
                throw new NotFoundException("Staff not found.");
            }

            var today = DateOnly.FromDateTime(VietnamTime.Now);
            var rows = await _context.TimesheetEntries.AsNoTracking()
                .Where(x => x.StaffId == staffId && x.WorkDate == today)
                .OrderByDescending(x => x.CheckInTime)
                .ToListAsync();
            var dayOfWeek = (int)today.ToDateTime(TimeOnly.MinValue).DayOfWeek;
            var branchIds = rows.Select(x => x.BranchId).Distinct().ToList();
            var operatingHours = branchIds.Count == 0
                ? new Dictionary<long, BranchOperatingHour>()
                : await _context.BranchOperatingHours.AsNoTracking()
                    .Where(x => branchIds.Contains(x.BranchId) && x.DayOfWeek == dayOfWeek)
                    .ToDictionaryAsync(x => x.BranchId);

            return rows.Select(entry =>
            {
                operatingHours.TryGetValue(entry.BranchId, out var schedule);
                return Map(entry, schedule);
            }).ToList();
        }

        public async Task<TimesheetEntryDto> CheckInAsync(TimesheetCheckInRequestDto request)
        {
            if (request == null)
            {
                throw new BadRequestException("Missing check-in payload.");
            }

            var staff = await _context.Staffs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.StaffId && x.IsDeleted != true);
            if (staff == null)
            {
                throw new NotFoundException("Staff not found.");
            }

            var resolvedBranchId = await ResolveBranchIdAsync(request, staff);
            var branchExists = await _context.Branches.AsNoTracking().AnyAsync(x => x.Id == resolvedBranchId);
            if (!branchExists)
            {
                throw new NotFoundException("Branch not found.");
            }

            var checkInTime = request.CheckInTime ?? VietnamTime.Now;
            var workDate = DateOnly.FromDateTime(checkInTime.Date);
            var workType = NormalizeWorkType(request.WorkType);
            var hasSameDayEntry = await _context.TimesheetEntries.AsNoTracking().AnyAsync(x =>
                x.StaffId == request.StaffId &&
                x.WorkDate == workDate &&
                x.WorkType == workType);
            if (hasSameDayEntry)
            {
                throw new BadRequestException("Bạn đã có bản ghi chấm công cho ca này trong hôm nay.");
            }

            var entry = new TimesheetEntry
            {
                StaffId = request.StaffId,
                BranchId = resolvedBranchId,
                WorkDate = workDate,
                CheckInTime = checkInTime,
                BreakMinutes = 0,
                WorkType = workType,
                Status = "Pending",
                Notes = NormalizeText(BuildCheckInNote(request), 1000),
                CreatedAt = VietnamTime.Now
            };

            try
            {
                _context.TimesheetEntries.Add(entry);
                await _context.SaveChangesAsync();
                var dayOfWeek = (int)entry.WorkDate.ToDateTime(TimeOnly.MinValue).DayOfWeek;
                var schedule = await _context.BranchOperatingHours.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.BranchId == entry.BranchId && x.DayOfWeek == dayOfWeek);
                return Map(entry, schedule);
            }
            catch (DbUpdateException)
            {
                throw new BadRequestException("Không thể check-in do bản ghi ca làm đã tồn tại.");
            }
        }

        private async Task<long> ResolveBranchIdAsync(TimesheetCheckInRequestDto request, Staff staff)
        {
            if (!request.Latitude.HasValue || !request.Longitude.HasValue)
            {
                throw new BadRequestException("Không lấy được GPS. Vui lòng bật định vị để check-in.");
            }

            var nearest = await FindNearestBranchAsync(request.Latitude.Value, request.Longitude.Value);
            if (nearest == null)
            {
                throw new BadRequestException("Hệ thống chưa cấu hình tọa độ chi nhánh.");
            }

            if (nearest.DistanceMeters > MaxCheckInDistanceMeters)
            {
                var distanceText = Math.Round(nearest.DistanceMeters, 0, MidpointRounding.AwayFromZero);
                throw new BadRequestException(
                    $"Bạn chưa ở đúng vị trí chi nhánh {nearest.Name}. Khoảng cách hiện tại {distanceText}m, yêu cầu <= {MaxCheckInDistanceMeters:0}m.");
            }

            return nearest.Id;
        }

        private async Task<NearestBranchInfo?> FindNearestBranchAsync(decimal latitude, decimal longitude)
        {
            var lat = Convert.ToDouble(latitude, CultureInfo.InvariantCulture);
            var lng = Convert.ToDouble(longitude, CultureInfo.InvariantCulture);

            var branches = await _context.Branches.AsNoTracking()
                .Where(x => x.IsDeleted != true && x.Lat.HasValue && x.Lng.HasValue)
                .Select(x => new { x.Id, x.Name, x.Lat, x.Lng })
                .ToListAsync();

            if (!branches.Any())
            {
                return null;
            }

            var nearest = branches
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    Distance = GetDistanceMeters(
                        lat,
                        lng,
                        Convert.ToDouble(x.Lat!.Value, CultureInfo.InvariantCulture),
                        Convert.ToDouble(x.Lng!.Value, CultureInfo.InvariantCulture))
                })
                .OrderBy(x => x.Distance)
                .First();

            return new NearestBranchInfo
            {
                Id = nearest.Id,
                Name = nearest.Name ?? $"#{nearest.Id}",
                DistanceMeters = nearest.Distance
            };
        }

        private static string? BuildCheckInNote(TimesheetCheckInRequestDto request)
        {
            var gpsNote = request.Latitude.HasValue && request.Longitude.HasValue
                ? $"GPS: {request.Latitude.Value.ToString("0.######", CultureInfo.InvariantCulture)}, {request.Longitude.Value.ToString("0.######", CultureInfo.InvariantCulture)}"
                : null;

            if (string.IsNullOrWhiteSpace(request.Notes)) return gpsNote;
            if (string.IsNullOrWhiteSpace(gpsNote)) return request.Notes;
            return $"{request.Notes}{Environment.NewLine}{gpsNote}";
        }

        private static double GetDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadius = 6371000d;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Pow(Math.Sin(dLat / 2), 2)
                    + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadius * c;
        }

        private static double ToRadians(double deg) => deg * Math.PI / 180d;

        private sealed class NearestBranchInfo
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public double DistanceMeters { get; set; }
        }

        public async Task<TimesheetEntryDto> CheckOutAsync(long timesheetId, TimesheetCheckOutRequestDto request)
        {
            if (timesheetId <= 0)
            {
                throw new BadRequestException("Invalid timesheet id.");
            }

            var entry = await _context.TimesheetEntries.FirstOrDefaultAsync(x => x.Id == timesheetId);
            if (entry == null)
            {
                throw new NotFoundException("Timesheet entry not found.");
            }

            if (entry.CheckOutTime.HasValue)
            {
                throw new BadRequestException("Timesheet entry is already checked out.");
            }

            var checkOutTime = request?.CheckOutTime ?? VietnamTime.Now;
            if (checkOutTime < entry.CheckInTime)
            {
                throw new BadRequestException("Check-out time cannot be before check-in time.");
            }

            var breakMinutes = request?.BreakMinutes ?? 0;
            if (breakMinutes < 0)
            {
                throw new BadRequestException("Break minutes cannot be negative.");
            }

            entry.CheckOutTime = checkOutTime;
            entry.BreakMinutes = breakMinutes;
            entry.Status = "Approved";
            if (!string.IsNullOrWhiteSpace(request?.Notes))
            {
                var note = NormalizeText(request.Notes, 1000);
                entry.Notes = string.IsNullOrWhiteSpace(entry.Notes)
                    ? note
                    : $"{entry.Notes}{Environment.NewLine}{note}";
            }

            await _context.SaveChangesAsync();
            var dayOfWeek = (int)entry.WorkDate.ToDateTime(TimeOnly.MinValue).DayOfWeek;
            var schedule = await _context.BranchOperatingHours.AsNoTracking()
                .FirstOrDefaultAsync(x => x.BranchId == entry.BranchId && x.DayOfWeek == dayOfWeek);
            return Map(entry, schedule);
        }

        private static string NormalizeWorkType(string? value)
        {
            var normalized = (value ?? "Regular").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "Regular";
            }

            if (normalized.Equals("Regular", StringComparison.OrdinalIgnoreCase))
            {
                return "Regular";
            }

            if (normalized.Equals("Overtime", StringComparison.OrdinalIgnoreCase))
            {
                return "Overtime";
            }

            if (normalized.Equals("Holiday", StringComparison.OrdinalIgnoreCase))
            {
                return "Holiday";
            }

            return "Regular";
        }

        private static string? NormalizeText(string? value, int maxLength)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }

        private static TimesheetEntryDto Map(TimesheetEntry entry, BranchOperatingHour? schedule)
        {
            decimal? netHours = null;
            if (entry.CheckOutTime.HasValue)
            {
                var totalMinutes = (entry.CheckOutTime.Value - entry.CheckInTime).TotalMinutes - entry.BreakMinutes;
                netHours = Math.Max(0m, Convert.ToDecimal(totalMinutes / 60d, CultureInfo.InvariantCulture));
            }

            var attendanceStatus = "UNKNOWN";
            int? checkInDeltaMinutes = null;
            string? scheduledStartTime = null;
            if (schedule != null && schedule.IsDayOff != true)
            {
                var scheduledStart = schedule.OpenTime.ToTimeSpan();
                var actualCheckIn = entry.CheckInTime.TimeOfDay;
                checkInDeltaMinutes = (int)Math.Round((actualCheckIn - scheduledStart).TotalMinutes, MidpointRounding.AwayFromZero);
                scheduledStartTime = schedule.OpenTime.ToString("HH:mm", CultureInfo.InvariantCulture);

                if (checkInDeltaMinutes <= -1)
                {
                    attendanceStatus = "EARLY";
                }
                else if (checkInDeltaMinutes <= OnTimeToleranceMinutes)
                {
                    attendanceStatus = "ON_TIME";
                }
                else
                {
                    attendanceStatus = "LATE";
                }
            }

            return new TimesheetEntryDto
            {
                Id = entry.Id,
                StaffId = entry.StaffId,
                BranchId = entry.BranchId,
                WorkDate = entry.WorkDate,
                CheckInTime = entry.CheckInTime,
                CheckOutTime = entry.CheckOutTime,
                BreakMinutes = entry.BreakMinutes,
                WorkType = entry.WorkType,
                Status = entry.Status,
                AttendanceStatus = attendanceStatus,
                ScheduledStartTime = scheduledStartTime,
                CheckInDeltaMinutes = checkInDeltaMinutes,
                Notes = entry.Notes,
                NetHours = netHours
            };
        }
    }
}
