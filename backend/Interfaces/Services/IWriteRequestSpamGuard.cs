namespace MyGarage.Interfaces;

public interface IWriteRequestSpamGuard
{
    bool IsCoolingDown(string key, DateTime nowUtc);
    bool TryEnter(string key, DateTime nowUtc, out string? rejectReason);
    void Exit(string key, DateTime nowUtc);
}
