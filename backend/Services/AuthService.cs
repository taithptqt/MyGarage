using System;
using System.Collections.Generic;
using System.Globalization;
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

        public AuthService(
            GarageContext context,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            var identifier = ResolveLoginIdentifier(request);
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new UnauthorizedAccessException("Invalid email/phone or password.");
            }

            var isEmailLogin = IsEmailIdentifier(identifier);
            var normalizedEmail = isEmailLogin ? NormalizeEmail(identifier) : string.Empty;
            var normalizedPhone = isEmailLogin ? string.Empty : NormalizePhone(identifier);

            var customer = isEmailLogin
                ? await _context.Customers.FirstOrDefaultAsync(c => c.Email == normalizedEmail)
                : await _context.Customers.FirstOrDefaultAsync(c => c.Phone == normalizedPhone);

            if (customer != null && VerifyPassword(request.Password, customer.PasswordHash, out var customerNeedsRehash))
            {
                ValidateCustomerAccountForLogin(customer);
                var nearestBranchId = await ResolveNearestBranchIdAsync(request.Lat, request.Lng);
                var nearestBranchName = await GetBranchNameAsync(nearestBranchId);
                AssignPreferredBranchToCustomer(customer, nearestBranchId);

                if (customerNeedsRehash)
                {
                    customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                }

                _context.Customers.Update(customer);
                await _context.SaveChangesAsync();

                return GenerateToken(
                    customer.Id.ToString(),
                    customer.FullName ?? "",
                    customer.Email ?? "",
                    "Customer",
                    nearestBranchId,
                    nearestBranchName);
            }

            var staff = isEmailLogin
                ? await _context.Staffs.FirstOrDefaultAsync(s => s.Email == normalizedEmail)
                : await _context.Staffs.FirstOrDefaultAsync(s => s.Phone == normalizedPhone);
            if (staff == null || !VerifyPassword(request.Password, staff.PasswordHash, out var staffNeedsRehash))
            {
                throw new UnauthorizedAccessException("Invalid email/phone or password.");
            }

            ValidateStaffAccountForLogin(staff);

            if (staffNeedsRehash)
            {
                staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                _context.Staffs.Update(staff);
                await _context.SaveChangesAsync();
            }

            var staffBranchName = await GetBranchNameAsync(staff.BranchId);
            return GenerateToken(
                staff.Id.ToString(),
                staff.FullName ?? "",
                staff.Email ?? "",
                "Staff",
                staff.BranchId,
                staffBranchName);
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
                var staffBranchName = await GetBranchNameAsync(staff.BranchId);
                return GenerateToken(
                    staff.Id.ToString(),
                    staff.FullName ?? "",
                    staff.Email ?? "",
                    "Staff",
                    staff.BranchId,
                    staffBranchName);
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
            var nearestBranchId = await ResolveNearestBranchIdAsync(request.Lat, request.Lng);
            var nearestBranchName = await GetBranchNameAsync(nearestBranchId);
            AssignPreferredBranchToCustomer(customer, nearestBranchId);
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
            return GenerateToken(
                customer.Id.ToString(),
                customer.FullName ?? "",
                customer.Email ?? "",
                "Customer",
                nearestBranchId,
                nearestBranchName);
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

            var nearestBranchId = await ResolveNearestBranchIdAsync(request.Lat, request.Lng, request.City);
            var nearestBranchName = await GetBranchNameAsync(nearestBranchId);
            AssignPreferredBranchToCustomer(customer, nearestBranchId);
            AssignCityToCustomer(customer, request.City);
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();

            return GenerateToken(
                customer.Id.ToString(),
                customer.FullName ?? "",
                customer.Email ?? "",
                "Customer",
                nearestBranchId,
                nearestBranchName);
        }

        private AuthResponseDto GenerateToken(string userId, string name, string email, string role, long? branchId = null, string branchName = "")
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyStr = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt Key missing");
            var key = Encoding.ASCII.GetBytes(keyStr);
            var expireDays = Convert.ToDouble(_configuration["Jwt:ExpireDays"] ?? "7");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };
            if (branchId.HasValue)
            {
                claims.Add(new Claim("branch_id", branchId.Value.ToString()));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(expireDays),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            return new AuthResponseDto
            {
                Id = long.TryParse(userId, out var uid) ? uid : 0,
                Token = tokenHandler.WriteToken(token),
                FullName = name,
                Email = email,
                Role = role,
                BranchId = branchId,
                BranchName = branchName ?? string.Empty
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
                    CreatedAt = customer.CreatedAt,
                    BranchId = ExtractPreferredBranchId(customer.Note),
                    BranchName = await GetBranchNameAsync(ExtractPreferredBranchId(customer.Note))
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
                    CreatedAt = staff.CreatedAt,
                    BranchId = staff.BranchId,
                    BranchName = await GetBranchNameAsync(staff.BranchId)
                };
            }

            throw new UnauthorizedAccessException("Role not recognized.");
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        private static bool IsEmailIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            return identifier.Contains('@');
        }

        private static string NormalizePhone(string phone)
        {
            return phone.Trim();
        }

        private static string ResolveLoginIdentifier(LoginRequestDto request)
        {
            if (!string.IsNullOrWhiteSpace(request.Identifier))
            {
                return request.Identifier.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                return request.Email.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                return request.Phone.Trim();
            }

            return string.Empty;
        }

        private async Task<long?> ResolveNearestBranchIdAsync(decimal? lat, decimal? lng, string? city = null)
        {
            var activeBranches = await _context.Branches
                .Where(b => b.IsDeleted != true)
                .Select(b => new { b.Id, b.Name, b.Lat, b.Lng })
                .ToListAsync();

            if (activeBranches.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(city) && TryResolveRegionFromProvince(city, out var region))
            {
                var regionKeywords = region switch
                {
                    "north" => new[] { "ha noi", "hanoi", "bac" },
                    "central" => new[] { "da nang", "danang", "trung" },
                    "south" => new[] { "hcm", "ho chi minh", "sai gon", "nam" },
                    _ => Array.Empty<string>()
                };

                var branchByRegion = activeBranches.FirstOrDefault(b =>
                    !string.IsNullOrWhiteSpace(b.Name) &&
                    regionKeywords.Any(k => NormalizeVietnamText(b.Name!).Contains(k, StringComparison.Ordinal)));

                if (branchByRegion is not null)
                {
                    return branchByRegion.Id;
                }
            }

            if (lat.HasValue && lng.HasValue)
            {
                var geoBranches = activeBranches
                    .Where(b => b.Lat.HasValue && b.Lng.HasValue)
                    .ToList();

                if (geoBranches.Count > 0)
                {
                    var nearest = geoBranches
                        .Select(b => new
                        {
                            b.Id,
                            Distance = CalculateDistanceKm(
                                (double)lat.Value,
                                (double)lng.Value,
                                (double)b.Lat!.Value,
                                (double)b.Lng!.Value)
                        })
                        .OrderBy(x => x.Distance)
                        .First();

                    return nearest.Id;
                }
            }

            return activeBranches.OrderBy(b => b.Id).Select(b => (long?)b.Id).FirstOrDefault();
        }

        private static bool TryResolveRegionFromProvince(string province, out string region)
        {
            var normalized = NormalizeVietnamText(province);
            normalized = normalized
                .Replace("tinh ", string.Empty, StringComparison.Ordinal)
                .Replace("thanh pho ", string.Empty, StringComparison.Ordinal)
                .Replace("tp ", string.Empty, StringComparison.Ordinal);

            var north = new HashSet<string>
            {
                "ha noi", "hai phong", "quang ninh", "bac ninh", "bac giang", "lang son", "cao bang",
                "bac kan", "thai nguyen", "tuyen quang", "ha giang", "lao cai", "yen bai", "dien bien",
                "lai chau", "son la", "hoa binh", "phu tho", "vinh phuc", "thai binh", "nam dinh",
                "ha nam", "ninh binh", "hung yen", "hai duong"
            };

            var central = new HashSet<string>
            {
                "thanh hoa", "nghe an", "ha tinh", "quang binh", "quang tri", "thua thien hue",
                "hue", "da nang", "quang nam", "quang ngai", "binh dinh", "phu yen", "khanh hoa",
                "ninh thuan", "binh thuan", "kon tum", "gia lai", "dak lak", "dak nong", "lam dong"
            };

            var south = new HashSet<string>
            {
                "ho chi minh", "tp hcm", "hcm", "sai gon", "ba ria vung tau", "vung tau",
                "binh duong", "binh phuoc", "dong nai", "tay ninh", "long an", "tien giang",
                "ben tre", "tra vinh", "vinh long", "dong thap", "an giang", "kien giang",
                "can tho", "hau giang", "soc trang", "bac lieu", "ca mau"
            };

            if (north.Contains(normalized))
            {
                region = "north";
                return true;
            }

            if (central.Contains(normalized))
            {
                region = "central";
                return true;
            }

            if (south.Contains(normalized))
            {
                region = "south";
                return true;
            }

            region = string.Empty;
            return false;
        }

        private static string NormalizeVietnamText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var buffer = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark) continue;
                buffer.Append(ch == '\u0111' ? 'd' : ch);
            }

            var noAccent = buffer.ToString().Normalize(NormalizationForm.FormC);
            var cleaned = new StringBuilder(noAccent.Length);
            var previousWasSpace = false;
            foreach (var ch in noAccent)
            {
                var current = char.IsLetterOrDigit(ch) ? ch : ' ';
                if (current == ' ')
                {
                    if (previousWasSpace) continue;
                    previousWasSpace = true;
                }
                else
                {
                    previousWasSpace = false;
                }
                cleaned.Append(current);
            }

            return cleaned.ToString().Trim();
        }

        private async Task<string> GetBranchNameAsync(long? branchId)
        {
            if (!branchId.HasValue) return string.Empty;
            return await _context.Branches
                       .Where(b => b.Id == branchId.Value)
                       .Select(b => b.Name ?? string.Empty)
                       .FirstOrDefaultAsync()
                   ?? string.Empty;
        }

        private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371d;
            static double ToRadians(double degree) => degree * Math.PI / 180d;

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a =
                Math.Pow(Math.Sin(dLat / 2d), 2d) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2d), 2d);
            var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
            return earthRadiusKm * c;
        }

        private static long? ExtractPreferredBranchId(string? note)
        {
            if (string.IsNullOrWhiteSpace(note)) return null;
            const string key = "[PreferredBranchId:";
            var start = note.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += key.Length;
            var end = note.IndexOf("]", start, StringComparison.OrdinalIgnoreCase);
            if (end <= start) return null;

            var raw = note.Substring(start, end - start);
            return long.TryParse(raw, out var value) ? value : null;
        }

        private static void AssignPreferredBranchToCustomer(Customer customer, long? branchId)
        {
            if (!branchId.HasValue) return;

            var existingBranchId = ExtractPreferredBranchId(customer.Note);
            if (existingBranchId == branchId.Value) return;

            var tag = $"[PreferredBranchId:{branchId.Value}]";
            if (string.IsNullOrWhiteSpace(customer.Note))
            {
                customer.Note = tag;
            }
            else if (existingBranchId.HasValue)
            {
                var oldTag = $"[PreferredBranchId:{existingBranchId.Value}]";
                customer.Note = customer.Note.Replace(oldTag, tag, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                customer.Note = $"{customer.Note} | {tag}";
            }
        }

        private static void AssignCityToCustomer(Customer customer, string? city)
        {
            var cleanedCity = city?.Trim();
            if (string.IsNullOrWhiteSpace(cleanedCity))
            {
                return;
            }

            var cityPrefix = "[City:";
            var cityStart = customer.Note?.IndexOf(cityPrefix, StringComparison.OrdinalIgnoreCase) ?? -1;
            var cityTag = $"[City:{cleanedCity}]";

            if (cityStart >= 0)
            {
                var end = customer.Note!.IndexOf("]", cityStart, StringComparison.OrdinalIgnoreCase);
                if (end > cityStart)
                {
                    var oldTag = customer.Note.Substring(cityStart, end - cityStart + 1);
                    customer.Note = customer.Note.Replace(oldTag, cityTag, StringComparison.OrdinalIgnoreCase);
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(customer.Note))
            {
                customer.Note = cityTag;
            }
            else
            {
                customer.Note = $"{customer.Note} | {cityTag}";
            }
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

