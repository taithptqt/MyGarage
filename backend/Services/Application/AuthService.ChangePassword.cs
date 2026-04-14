using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Auth;

namespace MyGarage.Services
{
    public partial class AuthService
    {
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
                customer.SecurityStamp = CreateSecurityStamp();
                ResetCustomerLockout(customer);
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
                staff.SecurityStamp = CreateSecurityStamp();
                ResetStaffLockout(staff);
                _context.Staffs.Update(staff);
                await _context.SaveChangesAsync();
            }
        }
    }
}
