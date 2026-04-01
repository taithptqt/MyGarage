using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyGarage.DTOs.Requests.Auth;

namespace MyGarage.Services
{
    public partial class AuthService
    {
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
                }, out _);

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
    }
}
