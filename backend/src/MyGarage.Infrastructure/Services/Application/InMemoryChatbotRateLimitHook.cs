using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class InMemoryChatbotRateLimitHook : IChatbotRateLimitHook
{
    private static readonly TimeSpan SessionWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MessageWindow = TimeSpan.FromSeconds(15);
    private const int MaxSessionsPerWindow = 3;
    private const int MaxMessagesPerWindow = 10;

    private readonly ConcurrentDictionary<string, SlidingCounter> _counters = new();
    private readonly ILogger<InMemoryChatbotRateLimitHook> _logger;

    public InMemoryChatbotRateLimitHook(ILogger<InMemoryChatbotRateLimitHook> logger)
    {
        _logger = logger;
    }

    public Task EnsureCanCreateSessionAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var key = $"session:{customerId}";
        EnsureWithinLimit(key, MaxSessionsPerWindow, SessionWindow, "SESSION_CREATE_RATE_LIMIT");
        return Task.CompletedTask;
    }

    public Task EnsureCanSendMessageAsync(long customerId, long sessionId, CancellationToken cancellationToken = default)
    {
        var key = $"message:{customerId}:{sessionId}";
        EnsureWithinLimit(key, MaxMessagesPerWindow, MessageWindow, "MESSAGE_RATE_LIMIT");
        return Task.CompletedTask;
    }

    private void EnsureWithinLimit(string key, int limit, TimeSpan window, string errorCode)
    {
        var now = DateTime.UtcNow;
        var counter = _counters.GetOrAdd(key, _ => new SlidingCounter());
        lock (counter.Gate)
        {
            if (now - counter.WindowStart >= window)
            {
                counter.WindowStart = now;
                counter.Count = 0;
            }

            counter.Count++;
            if (counter.Count <= limit)
            {
                return;
            }
        }

        _logger.LogWarning(
            "Chatbot rate limit exceeded. Key={RateLimitKey}, Limit={Limit}, WindowSeconds={WindowSeconds}",
            key,
            limit,
            window.TotalSeconds);

        throw new InvalidOperationException(errorCode);
    }

    private sealed class SlidingCounter
    {
        public object Gate { get; } = new();

        public DateTime WindowStart { get; set; } = DateTime.UtcNow;

        public int Count { get; set; }
    }
}
