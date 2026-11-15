using AutoMapper;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
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

        private readonly IStaffStore _store;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IMapper _mapper;

        public StaffService(
            IStaffRepository repository,
            IStaffStore store,
            IPasswordHasher passwordHasher,
            IMapper mapper) : base(repository)
        {
            _store = store;
            _passwordHasher = passwordHasher;
            _mapper = mapper;
        }

        public List<StaffResponseDto> MapToDtos(IEnumerable<Staff> items)
        {
            return _mapper.Map<List<StaffResponseDto>>(items);
        }

        public StaffResponseDto MapToDto(Staff item)
        {
            return _mapper.Map<StaffResponseDto>(item);
        }

        public async Task<StaffResponseDto> CreateDtoAsync(StaffCreateDto itemDto, long branchId)
        {
            var entity = _mapper.Map<Staff>(itemDto);
            entity.BranchId = branchId;
            var createdItem = await AddAsync(entity);
            return _mapper.Map<StaffResponseDto>(createdItem);
        }

        public async Task<StaffResponseDto?> UpdateDtoAsync(long id, StaffUpdateDto itemDto, long? scopedBranchId = null)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            if (scopedBranchId.HasValue)
            {
                existingItem.BranchId = scopedBranchId.Value;
            }

            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<StaffResponseDto>(updatedItem);
        }

        public override async System.Threading.Tasks.Task<Staff> AddAsync(Staff entity)
        {
            if (!string.IsNullOrEmpty(entity.PasswordHash))
            {
                entity.PasswordHash = _passwordHasher.HashPassword(entity.PasswordHash);
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
                entity.PasswordHash = _passwordHasher.HashPassword(entity.PasswordHash);
            }
            return await base.UpdateAsync(id, entity);
        }

        private async Task<Staff?> ResolveExistingStaffSnapshotAsync(object id)
        {
            if (!TryResolveStaffId(id, out var staffId))
            {
                return null;
            }

            return await _store.GetStaffSnapshotByIdAsync(staffId);
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
