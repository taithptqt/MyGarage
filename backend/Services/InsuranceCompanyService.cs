using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InsuranceCompanyService : GenericService<InsuranceCompany>, IInsuranceCompanyService
    {
        public InsuranceCompanyService(IInsuranceCompanyRepository repository) : base(repository)
        {
        }
    }
}
