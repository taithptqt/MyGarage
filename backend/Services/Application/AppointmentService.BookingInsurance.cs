using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task EnsureInsuranceCompanyExistsAsync(long? insuranceCompanyId)
        {
            if (!insuranceCompanyId.HasValue)
            {
                return;
            }

            if (insuranceCompanyId.Value <= 0)
            {
                throw new InvalidOperationException("Công ty bảo hiểm không hợp lệ.");
            }

            var exists = await _context.InsuranceCompanies
                .AsNoTracking()
                .AnyAsync(company => company.Id == insuranceCompanyId.Value && company.IsDeleted != true);

            if (!exists)
            {
                throw new InvalidOperationException("Không tìm thấy công ty bảo hiểm đã chọn.");
            }
        }
    }
}
