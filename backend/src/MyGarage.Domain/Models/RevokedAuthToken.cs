using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Models;

[Index(nameof(ExpiresAtUtc))]
public sealed class RevokedAuthToken
{
    [Key, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
