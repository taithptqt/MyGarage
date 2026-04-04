using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class SystemClock : ISystemClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime LocalNow => DateTime.Now;
        public DateOnly LocalToday => DateOnly.FromDateTime(DateTime.Now);
    }
}
