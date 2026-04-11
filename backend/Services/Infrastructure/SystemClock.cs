using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class SystemClock : ISystemClock
    {
        public DateTime UtcNow => VietnamTime.Now;
        public DateTime LocalNow => VietnamTime.Now;
        public DateOnly LocalToday => VietnamTime.Today;
    }
}
