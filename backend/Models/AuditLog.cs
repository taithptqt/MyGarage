using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AuditLog
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string UserType { get; set; } = null!;

    public long? BranchId { get; set; }

    public string Action { get; set; } = null!;

    public string TableName { get; set; } = null!;

    public long RecordId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? SessionId { get; set; }

    public DateTime? Timestamp { get; set; }

    public virtual Branch? Branch { get; set; }
}
