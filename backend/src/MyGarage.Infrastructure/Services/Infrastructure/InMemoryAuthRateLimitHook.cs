using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InMemoryAuthRateLimitHook : IAuthRateLimitHook
    {
        private static readonly TimeSpan ForgotPasswordWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ResetPasswordWindow = TimeSpan.FromMinutes(15);
        private const int ForgotPasswordLimit = 5;
        private const int ResetPasswordLimit = 10;

        private readonly ISystemClock _clock;
        private readonly ConcurrentDictionary<string, List<DateTime>> _forgotPasswordAttempts = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, List<DateTime>> _resetPasswordAttempts = new(StringComparer.Ordinal);

        public InMemoryAuthRateLimitHook(ISystemClock clock)
        {
            _clock = clock;
        }

        public Task<bool> CanRequestPasswordResetAsync(string normalizedEmail)
        {
            return Task.FromResult(RegisterAttempt(_forgotPasswordAttempts, normalizedEmail, ForgotPasswordWindow, ForgotPasswordLimit));
        }

        public Task<bool> CanSubmitPasswordResetAsync(string tokenFingerprint)
        {
            return Task.FromResult(RegisterAttempt(_resetPasswordAttempts, tokenFingerprint, ResetPasswordWindow, ResetPasswordLimit));
        }

        private bool RegisterAttempt(
            ConcurrentDictionary<string, List<DateTime>> store,
            string key,
            TimeSpan window,
            int limit)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var now = _clock.UtcNow;
            var attempts = store.GetOrAdd(key, _ => []);

            lock (attempts)
            {
                attempts.RemoveAll(timestamp => now - timestamp > window);
                if (attempts.Count >= limit)
                {
                    return false;
                }

                attempts.Add(now);
                return true;
            }
        }
    }
}
