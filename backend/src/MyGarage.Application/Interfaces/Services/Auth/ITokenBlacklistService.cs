namespace MyGarage.Interfaces;

public interface ITokenBlacklistService
{
    Task RevokeTokenAsync(string? rawToken, DateTime? expiresAtUtc = null);
    Task<bool> IsTokenRevokedAsync(string? rawToken);
    void RevokeUserSessions(string role, long userId);
    bool IsUserRevoked(string role, long userId, DateTime? issuedAtUtc);
}
