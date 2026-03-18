using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class NotificationTemplateService : GenericService<NotificationTemplate>, INotificationTemplateService
    {
        public NotificationTemplateService(INotificationTemplateRepository repository) : base(repository)
        {
        }
    }
}
