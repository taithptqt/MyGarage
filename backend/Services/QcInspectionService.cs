using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class QcInspectionService : GenericService<QcInspection>, IQcInspectionService
    {
        public QcInspectionService(IQcInspectionRepository repository) : base(repository)
        {
        }
    }
}
