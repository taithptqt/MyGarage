using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class InsuranceCompanyRepository : GenericRepository<InsuranceCompany>, IInsuranceCompanyRepository
    {
        public InsuranceCompanyRepository(IGarageContext context) : base(context)
        {
        }
    }
}

