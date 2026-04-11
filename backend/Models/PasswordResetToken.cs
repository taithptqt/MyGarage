using System;

namespace MyGarage.Models;

public class PasswordResetToken
{
    public long Id { get; set; }

    public long? CustomerId { get; set; }

    public long? StaffId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual Staff? Staff { get; set; }
}
