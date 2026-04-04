using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class NotificationTemplateRepository : GenericRepository<NotificationTemplate>, INotificationTemplateRepository
    {
        public NotificationTemplateRepository(IGarageContext context) : base(context)
        {
        }
    }
}

