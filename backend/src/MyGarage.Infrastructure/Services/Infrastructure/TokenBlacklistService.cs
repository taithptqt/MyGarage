using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using MyGarage.Interfaces;
using MyGarage.Data;
using MyGarage.Models;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services;

public sealed class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, DateTime> _revokedUsersAfterUtc = new(StringComparer.OrdinalIgnoreCase);
    private int _revocationsSinceCleanup;

    public TokenBlacklistService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task RevokeTokenAsync(string? rawToken, DateTime? expiresAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var effectiveExpiry = expiresAtUtc ?? ResolveJwtExpiryUtc(rawToken) ?? DateTime.UtcNow.AddDays(7);
        if (effectiveExpiry <= DateTime.UtcNow) return;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var hash = HashToken(rawToken);
        if (await db.RevokedAuthTokens.AnyAsync(x => x.TokenHash == hash)) return;
        db.RevokedAuthTokens.Add(new RevokedAuthToken { TokenHash = hash, ExpiresAtUtc = effectiveExpiry });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // Concurrent logout of the same token is successful if another request saved it.
            if (!await db.RevokedAuthTokens.AsNoTracking().AnyAsync(x => x.TokenHash == hash)) throw;
        }
        if (Interlocked.Increment(ref _revocationsSinceCleanup) % 128 == 0 && db.Database.IsRelational())
        {
            try
            {
                var now = DateTime.UtcNow;
                await db.RevokedAuthTokens.Where(x => x.ExpiresAtUtc <= now).ExecuteDeleteAsync();
            }
            catch (Exception ex)
            {
                scope.ServiceProvider.GetRequiredService<ILogger<TokenBlacklistService>>()
                    .LogWarning(ex, "Could not remove expired revoked tokens.");
            }
        }
    }

    public async Task<bool> IsTokenRevokedAsync(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return false;
        }

        var hash = HashToken(rawToken);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        return await db.RevokedAuthTokens.AsNoTracking()
            .AnyAsync(x => x.TokenHash == hash && x.ExpiresAtUtc > DateTime.UtcNow);
    }

    public void RevokeUserSessions(string role, long userId)
    {
        if (string.IsNullOrWhiteSpace(role) || userId <= 0)
        {
            return;
        }

        _revokedUsersAfterUtc[BuildUserKey(role, userId)] = DateTime.UtcNow;
    }

    public bool IsUserRevoked(string role, long userId, DateTime? issuedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(role) || userId <= 0)
        {
            return false;
        }

        if (!_revokedUsersAfterUtc.TryGetValue(BuildUserKey(role, userId), out var revokedAtUtc))
        {
            return false;
        }

        return !issuedAtUtc.HasValue || issuedAtUtc.Value <= revokedAtUtc;
    }

    private static DateTime? ResolveJwtExpiryUtc(string rawToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(rawToken))
            {
                var jwt = handler.ReadJwtToken(rawToken);
                return jwt.ValidTo == DateTime.MinValue ? null : jwt.ValidTo.ToUniversalTime();
            }
        }
        catch
        {
            // Ignore malformed tokens and fall back to default TTL
        }

        return null;
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim()));
        return Convert.ToHexString(bytes);
    }

    private static string BuildUserKey(string role, long userId) => $"{role.Trim().ToUpperInvariant()}:{userId}";
}
