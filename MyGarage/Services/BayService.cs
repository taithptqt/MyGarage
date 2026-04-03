using MyGarage.Models;
using MyGarage.Interfaces;
using System.Threading.Tasks;

namespace MyGarage.Services
{
    public class BayService : GenericService<Bay>, IBayService
    {
        public BayService(IBayRepository repository) : base(repository)
        {
        }

        public override async Task<Bay> AddAsync(Bay entity)
        {
            NormalizeServiceArea(entity);
            return await base.AddAsync(entity);
        }

        public override async Task<Bay?> UpdateAsync(object id, Bay entity)
        {
            NormalizeServiceArea(entity);
            return await base.UpdateAsync(id, entity);
        }

        private static void NormalizeServiceArea(Bay entity)
        {
            entity.ServiceArea = (entity.ServiceArea ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
