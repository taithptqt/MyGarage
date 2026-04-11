using System;

namespace MyGarage.Models;

public partial class ChatbotActionLog
{
    public long Id { get; set; }

    public long SessionId { get; set; }

    public string ActionType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? ActionPayload { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ChatbotSession Session { get; set; } = null!;
}
