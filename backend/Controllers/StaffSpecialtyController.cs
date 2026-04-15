using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [ApiController]
    public sealed class StaffSpecialtyController : ControllerBase
    {
        private readonly IGarageContext _context;

        public StaffSpecialtyController(IGarageContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpPost("api/staff/{id:long}/specialties")]
        [HttpPost("api/staffs/{id:long}/specialties")]
        public async Task<IActionResult> UpdateSpecialties(long id, [FromBody] StaffSpecialtyUpdateRequestDto? request)
        {
            var staff = await _context.Staffs.FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted != true);
            if (staff == null)
            {
                return NotFound(new { message = "Không tìm thấy nhân viên." });
            }

            var normalizedSpecialties = (request?.Specialties ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Where(x => x.Length <= 100)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            staff.Specialty = normalizedSpecialties.Count == 0
                ? null
                : string.Join(",", normalizedSpecialties);
            staff.UpdatedAt = VietnamTime.Now;
            staff.UpdatedBy = this.GetCurrentUserId();

            await _context.SaveChangesAsync();

            return Ok(new
            {
                staffId = staff.Id,
                specialty = staff.Specialty,
                specialties = normalizedSpecialties
            });
        }

        [HttpGet("api/staff/available")]
        [HttpGet("api/staffs/available")]
        public async Task<ActionResult<IEnumerable<StaffAvailabilityResponseDto>>> GetAvailableStaff(
            [FromQuery] string? specialty,
            [FromQuery] DateTime? date,
            [FromQuery] long? branchId = null)
        {
            var requestedDate = (date ?? VietnamTime.Now).Date;
            var dayStart = requestedDate;
            var dayEnd = requestedDate.AddDays(1);
            var normalizedSpecialty = NormalizeSpecialtyToken(specialty);

            var staffQuery = _context.Staffs
                .AsNoTracking()
                .Where(x => x.IsDeleted != true && (x.Status == null || x.Status != "INACTIVE"));

            if (branchId.HasValue && branchId.Value > 0)
            {
                staffQuery = staffQuery.Where(x => x.BranchId == branchId.Value);
            }

            if (!string.IsNullOrWhiteSpace(normalizedSpecialty))
            {
                staffQuery = staffQuery.Where(x =>
                    x.Specialty != null &&
                    EF.Functions.Like("," + x.Specialty + ",", "%," + normalizedSpecialty + ",%"));
            }

            var staffRows = await staffQuery
                .Select(x => new
                {
                    x.Id,
                    x.FullName,
                    x.BranchId,
                    x.TeamId,
                    x.Specialty
                })
                .ToListAsync();

            var assignedCounts = await _context.WorkSlots
                .AsNoTracking()
                .Where(x =>
                    x.TechnicianId.HasValue &&
                    x.StartTime < dayEnd &&
                    x.EndTime > dayStart &&
                    x.Status != "CANCELED")
                .GroupBy(x => x.TechnicianId!.Value)
                .Select(g => new
                {
                    TechnicianId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.TechnicianId, x => x.Count);

            var response = staffRows
                .Select(x =>
                {
                    assignedCounts.TryGetValue(x.Id, out var taskCount);
                    return new StaffAvailabilityResponseDto
                    {
                        StaffId = x.Id,
                        FullName = x.FullName ?? string.Empty,
                        BranchId = x.BranchId,
                        TeamId = x.TeamId,
                        Specialty = x.Specialty,
                        AssignedTaskCountInDate = taskCount,
                        IsBusyInDate = taskCount > 0
                    };
                })
                .OrderBy(x => x.IsBusyInDate)
                .ThenBy(x => x.AssignedTaskCountInDate)
                .ThenBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(response);
        }

        private static string NormalizeSpecialtyToken(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
