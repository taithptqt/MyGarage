namespace MyGarage.Interfaces
{
    public interface ISystemClock
    {
        DateTime UtcNow { get; }
        DateTime LocalNow { get; }
        DateOnly LocalToday { get; }
    }
}
