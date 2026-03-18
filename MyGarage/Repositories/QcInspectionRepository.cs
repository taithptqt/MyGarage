using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class QcInspectionRepository : GenericRepository<QcInspection>, IQcInspectionRepository
    {
        public QcInspectionRepository(GarageContext context) : base(context)
        {
        }
    }
}
