using System.Collections.Concurrent;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class InMemoryWriteRequestSpamGuard : IWriteRequestSpamGuard
{
    private readonly ConcurrentDictionary<string, DateTime> _inFlight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _recentDone = new(StringComparer.Ordinal);

    private const int CooldownMilliseconds = 1200;
    private const int MaxRecentEntries = 20000;

    public bool TryEnter(string key, DateTime nowUtc, out string? rejectReason)
    {
        rejectReason = null;
        Cleanup(nowUtc);

        if (_inFlight.TryAdd(key, nowUtc))
        {
            return true;
        }

        rejectReason = "inflight";
        return false;
    }

    public void Exit(string key, DateTime nowUtc)
    {
        _inFlight.TryRemove(key, out _);
        _recentDone[key] = nowUtc.AddMilliseconds(CooldownMilliseconds);
        Cleanup(nowUtc);
    }

    public bool IsCoolingDown(string key, DateTime nowUtc)
    {
        if (!_recentDone.TryGetValue(key, out var expiresAt))
        {
            return false;
        }

        if (expiresAt <= nowUtc)
        {
            _recentDone.TryRemove(key, out _);
            return false;
        }

        return true;
    }

    private void Cleanup(DateTime nowUtc)
    {
        if (_recentDone.Count <= MaxRecentEntries)
        {
            RemoveExpiredCooldowns(nowUtc);
            return;
        }

        RemoveExpiredCooldowns(nowUtc);

        if (_recentDone.Count <= MaxRecentEntries)
        {
            return;
        }

        foreach (var entry in _recentDone.OrderBy(x => x.Value).Take(_recentDone.Count - MaxRecentEntries))
        {
            _recentDone.TryRemove(entry.Key, out _);
        }
    }

    private void RemoveExpiredCooldowns(DateTime nowUtc)
    {
        foreach (var entry in _recentDone)
        {
            if (entry.Value <= nowUtc)
            {
                _recentDone.TryRemove(entry.Key, out _);
            }
        }
    }
}
