using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyGarage.Data;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AuthService : Interfaces.IAuthService
    {
        private readonly GarageContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public AuthService(GarageContext context, IConfiguration configuration, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            var normalizedEmail = NormalizeEmail(request.Email);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == normalizedEmail);

            if (customer != null && VerifyPassword(request.Password, customer.PasswordHash, out var customerNeedsRehash))
            {
                ValidateCustomerAccountForLogin(customer);

                if (customerNeedsRehash)
                {
                    customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                    _context.Customers.Update(customer);
                    await _context.SaveChangesAsync();
                }

                return GenerateToken(customer.Id.ToString(), customer.FullName ?? "", customer.Email ?? "", "Customer");
            }

            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.Email == normalizedEmail);
            if (staff == null || !VerifyPassword(request.Password, staff.PasswordHash, out var staffNeedsRehash))
            {
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            ValidateStaffAccountForLogin(staff);

            if (staffNeedsRehash)
            {
                staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                _context.Staffs.Update(staff);
                await _context.SaveChangesAsync();
            }

            return GenerateToken(staff.Id.ToString(), staff.FullName ?? "", staff.Email ?? "", "Staff");
        }

        public async Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginRequestDto request)
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new List<string> { _configuration["GoogleAuth:ClientId"] ?? throw new InvalidOperationException("Google ClientId missing") }
            };

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("Invalid Google Token.", ex);
            }

            // Check if user exists
            var normalizedEmail = NormalizeEmail(payload.Email);
            
            // 1. Check Staffs first
            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.Email == normalizedEmail);
            if (staff != null)
            {
                ValidateStaffAccountForLogin(staff);
                return GenerateToken(staff.Id.ToString(), staff.FullName ?? "", staff.Email ?? "", "Staff");
            }

            // 2. Check Customers
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == normalizedEmail);
            if (customer == null)
            {
                // Auto-register as Customer if not found in either table
                customer = new Customer
                {
                    Email = normalizedEmail,
                    FullName = payload.Name ?? "",
                    Phone = "",
                    PasswordHash = "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true,
                    IsDeleted = false
                };
            
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            ValidateCustomerAccountForLogin(customer);
            return GenerateToken(customer.Id.ToString(), customer.FullName ?? "", customer.Email ?? "", "Customer");
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            var normalizedEmail = NormalizeEmail(request.Email);

            if (await _context.Customers.AnyAsync(c => c.Email == normalizedEmail))
            {
                throw new InvalidOperationException("Email already exists.");
            }

            var customer = new Customer
            {
                FullName = request.FullName,
                Email = normalizedEmail,
                Phone = request.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return GenerateToken(customer.Id.ToString(), customer.FullName ?? "", customer.Email ?? "", "Customer");
        }

        private AuthResponseDto GenerateToken(string userId, string name, string email, string role)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyStr = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt Key missing");
            var key = Encoding.ASCII.GetBytes(keyStr);
            var expireDays = Convert.ToDouble(_configuration["Jwt:ExpireDays"] ?? "7");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Name, name),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Role, role)
                }),
                Expires = DateTime.UtcNow.AddDays(expireDays),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            return new AuthResponseDto
            {
                Token = tokenHandler.WriteToken(token),
                FullName = name,
                Email = email,
                Role = role
            };
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            // Verify if user exists
            var normalizedEmail = NormalizeEmail(request.Email);
            var isCustomer = await _context.Customers.AnyAsync(c => c.Email == normalizedEmail);
            bool isStaff = false;
            if (!isCustomer)
            {
                isStaff = await _context.Staffs.AnyAsync(s => s.Email == normalizedEmail);
            }

            if (!isCustomer && !isStaff)
            {
                // To prevent email enumeration, we do not throw an error if email doesn't exist.
                // In production, just return silently.
                return;
            }

            var role = isCustomer ? "Customer" : "Staff";
            
            // Create a short-lived reset token (e.g. 15 minutes)
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyStr = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt Key missing");
            var key = Encoding.ASCII.GetBytes(keyStr);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Email, request.Email),
                    new Claim(ClaimTypes.Role, role),
                    new Claim("purpose", "reset_password")
                }),
                Expires = DateTime.UtcNow.AddMinutes(15),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // Construct email body
            var resetLink = $"https://your-frontend-domain.com/reset-password?token={tokenString}";
            var emailBody = $"<h3>Password Reset Request</h3><p>Click the link below to reset your password. It will expire in 15 minutes.</p><a href='{resetLink}'>Reset Password</a><p>Or use this token directly: <br><code>{tokenString}</code></p>";

            await _emailService.SendEmailAsync(normalizedEmail, "MyGarage - Reset Password", emailBody);
        }

        public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyStr = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt Key missing");
            var key = Encoding.ASCII.GetBytes(keyStr);

            try
            {
                var principal = tokenHandler.ValidateToken(request.Token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true
                }, out SecurityToken validatedToken);

                var purpose = principal.FindFirst("purpose")?.Value;
                if (purpose != "reset_password")
                {
                    throw new UnauthorizedAccessException("Invalid token type.");
                }

                var email = principal.FindFirst(ClaimTypes.Email)?.Value;
                var role = principal.FindFirst(ClaimTypes.Role)?.Value;

                var normalizedEmail = NormalizeEmail(request.Email);

                if (email != normalizedEmail)
                {
                    throw new UnauthorizedAccessException("Token does not belong to the provided email.");
                }

                if (role == "Customer")
                {
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == email);
                    if (customer == null) throw new UnauthorizedAccessException("User not found.");
                    
                    customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                    _context.Customers.Update(customer);
                    await _context.SaveChangesAsync();
                }
                else if (role == "Staff")
                {
                    var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.Email == email);
                    if (staff == null) throw new UnauthorizedAccessException("User not found.");
                    
                    staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                    _context.Staffs.Update(staff);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("Invalid or expired reset token.", ex);
            }
        }

        public async Task ChangePasswordAsync(long userId, string role, ChangePasswordRequestDto request)
        {
            if (role == "Customer")
            {
                var customer = await _context.Customers.FindAsync(userId);
                if (customer == null) throw new UnauthorizedAccessException("User not found.");

                if (!VerifyPassword(request.OldPassword, customer.PasswordHash, out _))
                {
                    throw new UnauthorizedAccessException("Incorrect old password.");
                }

                customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                _context.Customers.Update(customer);
                await _context.SaveChangesAsync();
            }
            else if (role == "Staff")
            {
                var staff = await _context.Staffs.FindAsync(userId);
                if (staff == null) throw new UnauthorizedAccessException("User not found.");

                if (!VerifyPassword(request.OldPassword, staff.PasswordHash, out _))
                {
                    throw new UnauthorizedAccessException("Incorrect old password.");
                }

                staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                _context.Staffs.Update(staff);
                await _context.SaveChangesAsync();
            }
        }

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
                    Status = customer.IsActive == true ? "Active" : "Inactive",
                    CreatedAt = customer.CreatedAt
                };
            }
            else if (role == "Staff")
            {
                var staff = await _context.Staffs.FindAsync(userId);
                if (staff == null) throw new UnauthorizedAccessException("User not found.");

                return new UserProfileResponseDto
                {
                    Id = staff.Id,
                    FullName = staff.FullName ?? "",
                    Email = staff.Email ?? "",
                    Phone = staff.Phone ?? "",
                    Role = "Staff",
                    AvatarUrl = staff.AvatarUrl ?? "",
                    Status = staff.Status ?? "Unknown",
                    CreatedAt = staff.CreatedAt
                };
            }

            throw new UnauthorizedAccessException("Role not recognized.");
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        private static bool VerifyPassword(string inputPassword, string? storedPasswordHash, out bool needsRehash)
        {
            needsRehash = false;

            if (string.IsNullOrWhiteSpace(inputPassword) || string.IsNullOrWhiteSpace(storedPasswordHash))
            {
                return false;
            }

            if (IsBcryptHash(storedPasswordHash))
            {
                try
                {
                    return BCrypt.Net.BCrypt.Verify(inputPassword, storedPasswordHash);
                }
                catch
                {
                    return false;
                }
            }

            var matchedLegacyPlainText = string.Equals(inputPassword, storedPasswordHash, StringComparison.Ordinal);
            needsRehash = matchedLegacyPlainText;
            return matchedLegacyPlainText;
        }

        private static bool IsBcryptHash(string value)
        {
            return value.StartsWith("$2a$", StringComparison.Ordinal) ||
                   value.StartsWith("$2b$", StringComparison.Ordinal) ||
                   value.StartsWith("$2y$", StringComparison.Ordinal);
        }

        private static void ValidateCustomerAccountForLogin(Customer customer)
        {
            if (customer.IsDeleted == true || customer.IsActive == false)
            {
                throw new UnauthorizedAccessException("Account is inactive.");
            }

            if (customer.IsLockedOut == true && customer.LockoutEnd.HasValue && customer.LockoutEnd > DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Account is temporarily locked.");
            }
        }

        private static void ValidateStaffAccountForLogin(Staff staff)
        {
            if (staff.IsDeleted == true || string.Equals(staff.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Account is inactive.");
            }

            if (staff.IsLockedOut == true && staff.LockoutEnd.HasValue && staff.LockoutEnd > DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Account is temporarily locked.");
            }
        }
    }
}
