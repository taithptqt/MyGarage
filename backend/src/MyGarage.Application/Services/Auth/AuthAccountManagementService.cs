using System;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Auth;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class AuthAccountManagementService : IAuthAccountManagementService
    {
        private readonly IAuthCustomerAccountStore _customerStore;
        private readonly IAuthStaffAccountStore _staffStore;
        private readonly IAuthSessionValidationStore _sessionStore;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ISystemClock _clock;

        public AuthAccountManagementService(
            IAuthCustomerAccountStore customerStore,
            IAuthStaffAccountStore staffStore,
            IAuthSessionValidationStore sessionStore,
            IJwtTokenService jwtTokenService,
            IPasswordHasher passwordHasher,
            ISystemClock clock)
        {
            _customerStore = customerStore;
            _staffStore = staffStore;
            _sessionStore = sessionStore;
            _jwtTokenService = jwtTokenService;
            _passwordHasher = passwordHasher;
            _clock = clock;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            var normalizedEmail = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : AuthIdentityHelpers.NormalizeEmail(request.Email);

            if (!string.IsNullOrWhiteSpace(normalizedEmail)
                && await _staffStore.EmailExistsAsync(normalizedEmail))
            {
                throw new AuthRequestException("Email already exists.");
            }

            var normalizedPhone = AuthIdentityHelpers.NormalizeOptionalPhone(request.Phone);
            if (!string.IsNullOrWhiteSpace(normalizedPhone))
            {
                await AuthIdentityHelpers.EnsurePhoneAvailableAsync(_customerStore, _staffStore, normalizedPhone);
            }

            var customer = new Customer
            {
                FullName = request.FullName,
                Email = normalizedEmail,
                Phone = normalizedPhone ?? request.Phone,
                PasswordHash = _passwordHasher.HashPassword(request.Password),
                SecurityStamp = AuthIdentityHelpers.CreateSecurityStamp(),
                CreatedAt = _clock.UtcNow,
                UpdatedAt = _clock.UtcNow,
                IsActive = true
            };

            await _customerStore.AddCustomerInTransactionAsync(customer);

            var nearestBranchId = await AuthBranchLocationHelper.ResolveNearestBranchIdAsync(
                _sessionStore,
                request.Lat,
                request.Lng,
                request.City);
            var nearestBranchName = await _sessionStore.GetBranchNameAsync(nearestBranchId);
            AuthBranchLocationHelper.AssignPreferredBranchToCustomer(customer, nearestBranchId);
            AuthBranchLocationHelper.AssignCityToCustomer(customer, request.City);
            await _customerStore.UpdateCustomerInTransactionAsync(customer);

            return _jwtTokenService.GenerateToken(
                customer.Id.ToString(),
                customer.FullName ?? "",
                customer.Email ?? "",
                customer.Phone ?? "",
                "",
                "Customer",
                "CUSTOMER",
                customer.SecurityStamp ?? string.Empty,
                "Customer",
                nearestBranchId,
                nearestBranchName);
        }

        public async Task<UserProfileResponseDto> GetProfileAsync(long userId, string role)
        {
            if (role == "Customer")
            {
                var customer = await _customerStore.GetCustomerByIdAsync(userId);
                if (customer == null) throw new UnauthorizedAccessException("User not found.");

                var preferredBranchId = AuthBranchLocationHelper.ExtractPreferredBranchId(customer.Note);
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
                    BranchId = preferredBranchId,
                    BranchName = await _sessionStore.GetBranchNameAsync(preferredBranchId)
                };
            }

            if (role == "Staff")
            {
                var staff = await _staffStore.GetStaffByIdAsync(userId);
                if (staff == null) throw new UnauthorizedAccessException("User not found.");
                var (roleCode, roleName) = await _sessionStore.GetStaffRoleInfoAsync(staff.RoleId);

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
                    BranchName = await _sessionStore.GetBranchNameAsync(staff.BranchId)
                };
            }

            throw new UnauthorizedAccessException("Role not recognized.");
        }
    }
}
