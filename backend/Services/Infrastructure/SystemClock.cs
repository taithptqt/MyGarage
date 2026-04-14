using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class SystemClock : ISystemClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime LocalNow => VietnamTime.Now;
        public DateOnly LocalToday => VietnamTime.Today;
    }
}
