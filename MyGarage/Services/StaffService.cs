using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class StaffService : GenericService<Staff>, IStaffService
    {
        public StaffService(IStaffRepository repository) : base(repository)
        {
        }

        public override async System.Threading.Tasks.Task<Staff> AddAsync(Staff entity)
        {
            if (!string.IsNullOrEmpty(entity.PasswordHash))
            {
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(entity.PasswordHash);
            }
            return await base.AddAsync(entity);
        }

        public override async System.Threading.Tasks.Task<Staff?> UpdateAsync(object id, Staff entity)
        {
            if (!string.IsNullOrEmpty(entity.PasswordHash) && !entity.PasswordHash.StartsWith("$2a$") && !entity.PasswordHash.StartsWith("$2b$") && !entity.PasswordHash.StartsWith("$2y$"))
            {
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(entity.PasswordHash);
            }
            
            return await base.UpdateAsync(id, entity);
        }
    }
}
