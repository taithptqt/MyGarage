namespace MyGarage.Models;

public sealed class RevokedAuthToken
{
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
