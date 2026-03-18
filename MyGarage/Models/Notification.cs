using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Notification
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public bool? IsRead { get; set; }

    public DateTime? SentAt { get; set; }

    public string? LinkAction { get; set; }

    public virtual Customer Customer { get; set; } = null!;
}
