using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public partial class AuthService
    {
        public async Task<UserProfileResponseDto> GetProfileAsync(long userId, string role)
        {
            if (role == "Customer")
            {
                var customer = await _context.Customers.FindAsync(userId);
                if (customer == null) throw new UnauthorizedAccessException("User not found.");

                return new UserProfileResponseDto
                {
                    Id = customer.Id,
                    FullName = customer.FullName ?? "",
                    Email = customer.Email ?? "",
                    Phone = customer.Phone ?? "",
                    Role = "Customer",
                    RoleCode = "CUSTOMER",
                    RoleName = "Customer",
                    Status = customer.IsActive == true ? nameof(ProfileAccountStatus.Active) : nameof(ProfileAccountStatus.Inactive),
                    CreatedAt = customer.CreatedAt,
                    BranchId = ExtractPreferredBranchId(customer.Note),
                    BranchName = await GetBranchNameAsync(ExtractPreferredBranchId(customer.Note))
                };
            }
            else if (role == "Staff")
            {
                var staff = await _context.Staffs.FindAsync(userId);
                if (staff == null) throw new UnauthorizedAccessException("User not found.");
                var (roleCode, roleName) = await GetStaffRoleInfoAsync(staff.RoleId);

                return new UserProfileResponseDto
                {
                    Id = staff.Id,
                    FullName = staff.FullName ?? "",
                    Email = staff.Email ?? "",
                    Phone = staff.Phone ?? "",
                    Role = "Staff",
                    RoleCode = roleCode,
                    RoleName = roleName,
                    AvatarUrl = staff.AvatarUrl ?? "",
                    Status = staff.Status ?? nameof(ProfileAccountStatus.Unknown),
                    CreatedAt = staff.CreatedAt,
                    BranchId = staff.BranchId,
                    BranchName = await GetBranchNameAsync(staff.BranchId)
                };
            }

            throw new UnauthorizedAccessException("Role not recognized.");
        }
    }
}
