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
        public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            var normalizedEmail = NormalizeEmail(request.Email);
            var isCustomer = await _context.Customers.AnyAsync(c => c.Email == normalizedEmail);
            bool isStaff = false;
            if (!isCustomer)
            {
                isStaff = await _context.Staffs.AnyAsync(s => s.Email == normalizedEmail);
            }

            if (!isCustomer && !isStaff)
            {
                return;
            }

            var role = isCustomer ? "Customer" : "Staff";
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

            var resetLink = $"https://your-frontend-domain.com/reset-password?token={tokenString}";
            var emailBody =
                $"<h3>Password Reset Request</h3><p>Click the link below to reset your password. It will expire in 15 minutes.</p><a href='{resetLink}'>Reset Password</a><p>Or use this token directly: <br><code>{tokenString}</code></p>";

            await _emailService.SendEmailAsync(normalizedEmail, "MyGarage - Reset Password", emailBody);
        }
    }
}
