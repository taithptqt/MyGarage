using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class MaterialReturnService : GenericService<MaterialReturn>, IMaterialReturnService
    {
        public MaterialReturnService(IMaterialReturnRepository repository) : base(repository)
        {
        }
    }
}
