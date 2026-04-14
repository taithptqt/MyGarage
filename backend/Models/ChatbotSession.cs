using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ChatbotSession
{
    public long Id { get; set; }

    public string SessionToken { get; set; } = null!;

    public long CustomerId { get; set; }

    public long? BranchId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime LastActivityAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? CurrentIntent { get; set; }

    public string? IntentState { get; set; }

    public long? ContextVehicleId { get; set; }

    public long? ContextServiceCategoryId { get; set; }

    public long? HandoffConversationId { get; set; }

    public string? HandoffSummary { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Vehicle? ContextVehicle { get; set; }

    public virtual ServiceCategory? ContextServiceCategory { get; set; }

    public virtual Conversation? HandoffConversation { get; set; }

    public virtual ICollection<ChatbotMessage> ChatbotMessages { get; set; } = new List<ChatbotMessage>();

    public virtual ICollection<ChatbotActionLog> ChatbotActionLogs { get; set; } = new List<ChatbotActionLog>();
}
