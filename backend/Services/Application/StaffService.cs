using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class StaffService : GenericService<Staff>, IStaffService
    {
        private static readonly HashSet<string> SessionInvalidatingStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(StaffStatusCode.SUSPENDED),
            nameof(StaffStatusCode.TERMINATED),
        };

        private readonly IGarageContext _context;

        public StaffService(IStaffRepository repository, IGarageContext context) : base(repository)
        {
            _context = context;
        }

        public override async System.Threading.Tasks.Task<Staff> AddAsync(Staff entity)
        {
            if (!string.IsNullOrEmpty(entity.PasswordHash))
            {
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(entity.PasswordHash);
            }
            return await base.AddAsync(entity);
        }

        public override async System.Threading.Tasks.Task<Staff?> UpdateAsync(object id, Staff entity)
        {
            var existingStaff = await ResolveExistingStaffSnapshotAsync(id);
            if (existingStaff == null)
            {
                return null;
            }

            if (ShouldInvalidateSessions(existingStaff, entity))
            {
                entity.SecurityStamp = CreateSecurityStamp();
            }

            if (!string.IsNullOrEmpty(entity.PasswordHash) && !entity.PasswordHash.StartsWith("$2a$") && !entity.PasswordHash.StartsWith("$2b$") && !entity.PasswordHash.StartsWith("$2y$"))
            {
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(entity.PasswordHash);
            }
            return await base.UpdateAsync(id, entity);
        }

        public override async Task<IEnumerable<Staff>> GetAllAsync()
        {
            return await _context.Staffs
                .Where(staff => staff.IsDeleted != true)
                .ToListAsync();
        }

        public override async Task<Staff?> GetByIdAsync(object id)
        {
            if (!TryResolveStaffId(id, out var staffId))
            {
                return null;
            }

            return await _context.Staffs
                .FirstOrDefaultAsync(staff => staff.Id == staffId && staff.IsDeleted != true);
        }

        public override async Task<bool> DeleteAsync(object id)
        {
            if (!TryResolveStaffId(id, out var staffId))
            {
                return false;
            }

            var staff = await _context.Staffs
                .FirstOrDefaultAsync(item => item.Id == staffId && item.IsDeleted != true);
            if (staff == null)
            {
                return false;
            }

            staff.IsDeleted = true;
            staff.DeletedAt = VietnamTime.Now;
            staff.Status = nameof(StaffStatusCode.TERMINATED);
            staff.SecurityStamp = CreateSecurityStamp();
            staff.UpdatedAt = VietnamTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<Staff?> ResolveExistingStaffSnapshotAsync(object id)
        {
            if (!TryResolveStaffId(id, out var staffId))
            {
                return null;
            }

            return await _context.Staffs
                .AsNoTracking()
                .SingleOrDefaultAsync(staff => staff.Id == staffId);
        }

        private static bool TryResolveStaffId(object id, out long staffId)
        {
            switch (id)
            {
                case long value:
                    staffId = value;
                    return true;
                case int value:
                    staffId = value;
                    return true;
                case string text when long.TryParse(text, out var parsed):
                    staffId = parsed;
                    return true;
                default:
                    staffId = default;
                    return false;
            }
        }

        private static bool ShouldInvalidateSessions(Staff existingStaff, Staff updatedStaff)
        {
            if (existingStaff.RoleId != updatedStaff.RoleId)
            {
                return true;
            }

            if (existingStaff.IsDeleted != true && updatedStaff.IsDeleted == true)
            {
                return true;
            }

            var existingStatus = NormalizeStatus(existingStaff.Status);
            var updatedStatus = NormalizeStatus(updatedStaff.Status);
            if (!string.Equals(existingStatus, updatedStatus, StringComparison.Ordinal) &&
                SessionInvalidatingStatuses.Contains(updatedStatus))
            {
                return true;
            }

            if (!HasEffectiveTermination(existingStaff.TerminationDate) && HasEffectiveTermination(updatedStaff.TerminationDate))
            {
                return true;
            }

            return false;
        }

        private static string NormalizeStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status)
                ? string.Empty
                : status.Trim().ToUpperInvariant();
        }

        private static bool HasEffectiveTermination(DateOnly? terminationDate)
        {
            if (!terminationDate.HasValue)
            {
                return false;
            }

            return terminationDate.Value <= VietnamTime.Today;
        }

        private static string CreateSecurityStamp()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
