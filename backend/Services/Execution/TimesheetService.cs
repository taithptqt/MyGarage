using System;
using System.Globalization;
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
        private readonly IGarageContext _context;

        public TimesheetService(IGarageContext context)
        {
            _context = context;
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

            var branchExists = await _context.Branches.AsNoTracking().AnyAsync(x => x.Id == request.BranchId);
            if (!branchExists)
            {
                throw new NotFoundException("Branch not found.");
            }

            var checkInTime = request.CheckInTime ?? VietnamTime.Now;
            var workDate = DateOnly.FromDateTime(checkInTime.Date);
            var workType = NormalizeWorkType(request.WorkType);
            var hasOpenEntry = await _context.TimesheetEntries.AsNoTracking().AnyAsync(x =>
                x.StaffId == request.StaffId &&
                x.WorkDate == workDate &&
                x.WorkType == workType &&
                x.CheckOutTime == null);
            if (hasOpenEntry)
            {
                throw new BadRequestException("An active timesheet entry already exists.");
            }

            var entry = new TimesheetEntry
            {
                StaffId = request.StaffId,
                BranchId = request.BranchId,
                WorkDate = workDate,
                CheckInTime = checkInTime,
                BreakMinutes = 0,
                WorkType = workType,
                Status = "Pending",
                Notes = NormalizeText(request.Notes, 1000),
                CreatedAt = VietnamTime.Now
            };

            _context.TimesheetEntries.Add(entry);
            await _context.SaveChangesAsync();
            return Map(entry);
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
            return Map(entry);
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

        private static TimesheetEntryDto Map(TimesheetEntry entry)
        {
            decimal? netHours = null;
            if (entry.CheckOutTime.HasValue)
            {
                var totalMinutes = (entry.CheckOutTime.Value - entry.CheckInTime).TotalMinutes - entry.BreakMinutes;
                netHours = Math.Max(0m, Convert.ToDecimal(totalMinutes / 60d, CultureInfo.InvariantCulture));
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
                Notes = entry.Notes,
                NetHours = netHours
            };
        }
    }
}
