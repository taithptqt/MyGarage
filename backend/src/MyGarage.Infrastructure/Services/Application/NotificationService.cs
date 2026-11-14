using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class NotificationService : GenericService<Notification>, INotificationService
    {
        public NotificationService(INotificationRepository repository) : base(repository)
        {
        }
    }
}
