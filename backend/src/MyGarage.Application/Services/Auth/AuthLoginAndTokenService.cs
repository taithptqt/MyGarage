using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Auth;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed record AuthAccountStores(
        IAuthCustomerAccountStore Customers,
        IAuthStaffAccountStore Staff,
        IAuthSessionValidationStore Sessions);

    public sealed class AuthLoginAndTokenService : IAuthLoginAndTokenService
    {
        private const string PhoneRequestCode = "PHONE_REQUEST";
        private static readonly Regex GooglePhoneRegex = new(@"^(0\d{9}|\+84\d{9})$", RegexOptions.Compiled);

        private readonly IAuthCustomerAccountStore _customerStore;
        private readonly IAuthStaffAccountStore _staffStore;
        private readonly IAuthSessionValidationStore _sessionStore;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IGoogleTokenVerifier _googleTokenVerifier;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ISystemClock _clock;

        public AuthLoginAndTokenService(
            AuthAccountStores stores,
            IJwtTokenService jwtTokenService,
            IGoogleTokenVerifier googleTokenVerifier,
            IPasswordHasher passwordHasher,
            ISystemClock clock)
        {
            _customerStore = stores.Customers;
            _staffStore = stores.Staff;
            _sessionStore = stores.Sessions;
            _jwtTokenService = jwtTokenService;
            _googleTokenVerifier = googleTokenVerifier;
            _passwordHasher = passwordHasher;
            _clock = clock;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            var identifier = AuthIdentityHelpers.ResolveLoginIdentifier(request);
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new UnauthorizedAccessException("Invalid email/phone or password.");
            }

            var isEmailLogin = AuthIdentityHelpers.IsEmailIdentifier(identifier);
            var normalizedEmail = isEmailLogin ? AuthIdentityHelpers.NormalizeEmail(identifier) : string.Empty;
            var normalizedPhone = isEmailLogin ? string.Empty : AuthIdentityHelpers.NormalizePhone(identifier);

            var customer = isEmailLogin
                ? await _customerStore.GetCustomerByEmailAsync(normalizedEmail)
                : await _customerStore.GetCustomerByPhoneAsync(normalizedPhone);

            if (customer != null && AuthIdentityHelpers.VerifyPassword(_passwordHasher, request.Password, customer.PasswordHash, out var customerNeedsRehash))
            {
                AuthIdentityHelpers.ValidateCustomerAccountForLogin(customer);
                var nearestBranchId = await AuthBranchLocationHelper.ResolveNearestBranchIdAsync(_sessionStore, request.Lat, request.Lng);
                var nearestBranchName = await _sessionStore.GetBranchNameAsync(nearestBranchId);
                AuthBranchLocationHelper.AssignPreferredBranchToCustomer(customer, nearestBranchId);
                var customerSecurityStamp = AuthIdentityHelpers.GetOrCreateSecurityStamp(customer.SecurityStamp);

                if (customerNeedsRehash || !string.Equals(customer.SecurityStamp, customerSecurityStamp, StringComparison.Ordinal))
                {
                    customer.SecurityStamp = customerSecurityStamp;
                    if (customerNeedsRehash)
                    {
                        customer.PasswordHash = _passwordHasher.HashPassword(request.Password);
                    }
                }

                await _customerStore.UpdateCustomerInTransactionAsync(customer);

                return _jwtTokenService.GenerateToken(
                    customer.Id.ToString(),
                    customer.FullName ?? "",
                    customer.Email ?? "",
                    customer.Phone ?? "",
                    "",
                    "Customer",
                    "CUSTOMER",
                    customerSecurityStamp,
                    "Customer",
                    nearestBranchId,
                    nearestBranchName);
            }

            var staff = isEmailLogin
                ? await _staffStore.GetStaffByEmailAsync(normalizedEmail)
                : await _staffStore.GetStaffByPhoneAsync(normalizedPhone);
            if (staff == null || !AuthIdentityHelpers.VerifyPassword(_passwordHasher, request.Password, staff.PasswordHash, out var staffNeedsRehash))
            {
                throw new UnauthorizedAccessException("Invalid email/phone or password.");
            }

            AuthIdentityHelpers.ValidateStaffAccountForLogin(staff);
            var staffSecurityStamp = AuthIdentityHelpers.GetOrCreateSecurityStamp(staff.SecurityStamp);

            if (staffNeedsRehash || !string.Equals(staff.SecurityStamp, staffSecurityStamp, StringComparison.Ordinal))
            {
                staff.SecurityStamp = staffSecurityStamp;
                if (staffNeedsRehash)
                {
                    staff.PasswordHash = _passwordHasher.HashPassword(request.Password);
                }

                await _staffStore.UpdateStaffInTransactionAsync(staff);
            }

            var (roleCode, roleName) = await _sessionStore.GetStaffRoleInfoAsync(staff.RoleId);
            var staffBranchName = await _sessionStore.GetBranchNameAsync(staff.BranchId);
            return _jwtTokenService.GenerateToken(
                staff.Id.ToString(),
                staff.FullName ?? "",
                staff.Email ?? "",
                staff.Phone ?? "",
                staff.AvatarUrl ?? "",
                "Staff",
                roleCode,
                staffSecurityStamp,
                roleName,
                staff.BranchId,
                staffBranchName);
        }

        public async Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginRequestDto request)
        {
            var payload = await _googleTokenVerifier.ValidateAsync(request.IdToken);
            return await GoogleLoginValidatedAsync(payload, request);
        }

        // Only call after Google's signature, issuer, audience and expiry validation.
        public async Task<AuthResponseDto> GoogleLoginValidatedAsync(GoogleTokenPayload payload, GoogleLoginRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(payload.Email))
            {
                throw new UnauthorizedAccessException("Google account does not provide email.");
            }

            if (!payload.EmailVerified)
            {
                throw new UnauthorizedAccessException("Google account email is not verified.");
            }

            if (string.IsNullOrWhiteSpace(payload.Subject))
            {
                throw new UnauthorizedAccessException("Google account identifier is missing.");
            }

            var normalizedEmail = AuthIdentityHelpers.NormalizeEmail(payload.Email);
            var googleId = payload.Subject.Trim();
            var normalizedPhone = AuthIdentityHelpers.NormalizeOptionalPhone(request.Phone);

            if (!string.IsNullOrWhiteSpace(normalizedPhone) && !GooglePhoneRegex.IsMatch(normalizedPhone))
            {
                throw new AuthRequestException("Số điện thoại không đúng định dạng (0xxxxxxxxx hoặc +84xxxxxxxxx).");
            }

            var staff = await _staffStore.GetStaffByGoogleIdAsync(googleId)
                ?? await _staffStore.GetStaffByEmailAsync(normalizedEmail);

            if (staff != null)
            {
                return await CompleteStaffGoogleLoginAsync(staff, googleId, normalizedPhone);
            }

            var customer = await _customerStore.GetCustomerByGoogleIdAsync(googleId)
                ?? await _customerStore.GetCustomerByEmailAsync(normalizedEmail);

            if (customer == null)
            {
                if (!string.IsNullOrWhiteSpace(normalizedPhone))
                {
                    await AuthIdentityHelpers.EnsurePhoneAvailableAsync(_customerStore, _staffStore, normalizedPhone);
                }

                customer = new Customer
                {
                    Email = normalizedEmail,
                    GoogleId = googleId,
                    FullName = payload.Name ?? "",
                    Type = "INDIVIDUAL",
                    Phone = normalizedPhone,
                    PasswordHash = "",
                    SecurityStamp = AuthIdentityHelpers.CreateSecurityStamp(),
                    CreatedAt = _clock.UtcNow,
                    UpdatedAt = _clock.UtcNow,
                    IsActive = true,
                    IsDeleted = false
                };

                await _customerStore.AddCustomerInTransactionAsync(customer);
            }
            else
            {
                AuthIdentityHelpers.ValidateCustomerAccountForLogin(customer);
                EnsureGoogleIdentityMatches(customer.GoogleId, googleId);
                if (string.IsNullOrWhiteSpace(customer.GoogleId))
                {
                    customer.GoogleId = googleId;
                    customer.UpdatedAt = DateTime.UtcNow;
                    await _customerStore.UpdateCustomerInTransactionAsync(customer);
                }
            }

            AuthIdentityHelpers.ValidateCustomerAccountForLogin(customer);

            if (string.IsNullOrWhiteSpace(customer.Phone))
            {
                if (string.IsNullOrWhiteSpace(normalizedPhone))
                {
                    throw new UnauthorizedAccessException($"{PhoneRequestCode}:CUSTOMER");
                }

                await AuthIdentityHelpers.EnsurePhoneAvailableAsync(
                    _customerStore,
                    _staffStore,
                    normalizedPhone,
                    excludingCustomerId: customer.Id);
                customer.Phone = normalizedPhone;
                customer.UpdatedAt = DateTime.UtcNow;
            }

            var nearestBranchId = await AuthBranchLocationHelper.ResolveNearestBranchIdAsync(_sessionStore, request.Lat, request.Lng);
            var nearestBranchName = await _sessionStore.GetBranchNameAsync(nearestBranchId);
            AuthBranchLocationHelper.AssignPreferredBranchToCustomer(customer, nearestBranchId);
            customer.SecurityStamp = AuthIdentityHelpers.GetOrCreateSecurityStamp(customer.SecurityStamp);
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

        private async Task<AuthResponseDto> CompleteStaffGoogleLoginAsync(
            Staff staff,
            string googleId,
            string? normalizedPhone)
        {
            AuthIdentityHelpers.ValidateStaffAccountForLogin(staff);
            EnsureGoogleIdentityMatches(staff.GoogleId, googleId);
            var nowUtc = DateTime.UtcNow;
            var shouldSaveStaff = false;
            var staffSecurityStamp = AuthIdentityHelpers.GetOrCreateSecurityStamp(staff.SecurityStamp);

            if (!string.Equals(staff.SecurityStamp, staffSecurityStamp, StringComparison.Ordinal))
            {
                staff.SecurityStamp = staffSecurityStamp;
                shouldSaveStaff = true;
            }

            if (!string.Equals(staff.GoogleId, googleId, StringComparison.Ordinal))
            {
                staff.GoogleId = googleId;
                shouldSaveStaff = true;
            }

            if (string.IsNullOrWhiteSpace(staff.Phone))
            {
                if (string.IsNullOrWhiteSpace(normalizedPhone))
                {
                    if (shouldSaveStaff)
                    {
                        staff.UpdatedAt = nowUtc;
                        await _staffStore.UpdateStaffInTransactionAsync(staff);
                    }

                    throw new UnauthorizedAccessException($"{PhoneRequestCode}:STAFF");
                }

                await AuthIdentityHelpers.EnsurePhoneAvailableAsync(
                    _customerStore,
                    _staffStore,
                    normalizedPhone,
                    excludingStaffId: staff.Id);
                staff.Phone = normalizedPhone;
                shouldSaveStaff = true;
            }

            if (shouldSaveStaff)
            {
                staff.UpdatedAt = nowUtc;
                await _staffStore.UpdateStaffInTransactionAsync(staff);
            }

            var (roleCode, roleName) = await _sessionStore.GetStaffRoleInfoAsync(staff.RoleId);
            var staffBranchName = await _sessionStore.GetBranchNameAsync(staff.BranchId);
            return _jwtTokenService.GenerateToken(
                staff.Id.ToString(),
                staff.FullName ?? "",
                staff.Email ?? "",
                staff.Phone ?? "",
                staff.AvatarUrl ?? "",
                "Staff",
                roleCode,
                staffSecurityStamp,
                roleName,
                staff.BranchId,
                staffBranchName);
        }

        internal static void EnsureGoogleIdentityMatches(string? existingGoogleId, string incomingGoogleId)
        {
            if (!string.IsNullOrWhiteSpace(existingGoogleId) &&
                !string.Equals(existingGoogleId, incomingGoogleId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Google account is already linked to a different identity.");
            }
        }
    }
}
