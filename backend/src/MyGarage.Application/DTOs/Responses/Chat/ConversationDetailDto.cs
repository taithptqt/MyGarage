using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Chat
{
    public class ConversationDetailDto : ConversationListItemDto
    {
        public long? AppointmentId { get; set; }
        public long? ServiceOrderId { get; set; }
        public long? VehicleId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public IReadOnlyList<ChatMessageDto>? Messages { get; set; }
    }
}
