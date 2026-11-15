using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MyGarage.DTOs.Responses.Auth;

namespace MyGarage.Services
{
    public partial class AuthService
    {
        private AuthResponseDto GenerateToken(
            string userId,
            string name,
            string email,
            string phone,
            string avatarUrl,
            string role,
            string roleCode,
            string securityStamp,
            string roleName,
            long? branchId = null,
            string branchName = "")
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyStr = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt Key missing");
            var key = Encoding.UTF8.GetBytes(keyStr);
            if (key.Length < 32)
            {
                throw new InvalidOperationException("Jwt:Key must be at least 32 UTF-8 bytes.");
            }
            var expireDays = Convert.ToDouble(_configuration["Jwt:ExpireDays"] ?? "7");

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("auth_time_ticks", DateTime.UtcNow.Ticks.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim("role_code", roleCode ?? string.Empty),
                new Claim("role_name", roleName ?? string.Empty),
                new Claim("security_stamp", securityStamp ?? string.Empty)
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
                Phone = phone,
                AvatarUrl = avatarUrl,
                Role = role,
                RoleCode = roleCode ?? string.Empty,
                RoleName = roleName ?? string.Empty,
                BranchId = branchId,
                BranchName = branchName ?? string.Empty
            };
        }


    }
}
