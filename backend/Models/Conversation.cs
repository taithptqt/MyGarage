using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Conversation
{
    public long Id { get; set; }

    public long? StaffId { get; set; }

    public long? BranchId { get; set; }

    public long CustomerId { get; set; }

    public long? AppointmentId { get; set; }

    public long? ServiceOrderId { get; set; }

    public long? VehicleId { get; set; }

    public long? AssignedStaffId { get; set; }

    public string? LastMessage { get; set; }

    public long? LastMessageId { get; set; }

    public long? LastSenderId { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Status { get; set; }

    public byte[]? RowVersion { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual Staff? AssignedStaff { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual ICollection<ConversationCustomerRead> ConversationCustomerReads { get; set; } = new List<ConversationCustomerRead>();

    public virtual ICollection<ConversationStaffRead> ConversationStaffReads { get; set; } = new List<ConversationStaffRead>();

    public virtual Customer Customer { get; set; } = null!;

    public virtual ChatMessage? LastMessageNavigation { get; set; }

    public virtual ServiceOrder? ServiceOrder { get; set; }

    public virtual Staff? Staff { get; set; }

    public virtual Vehicle? Vehicle { get; set; }
}
