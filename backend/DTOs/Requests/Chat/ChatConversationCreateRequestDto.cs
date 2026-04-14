namespace MyGarage.DTOs.Requests.Chat
{
    public class ChatConversationCreateRequestDto
    {
        public long BranchId { get; set; }
        public long? AppointmentId { get; set; }
        public long? ServiceOrderId { get; set; }
        public long? VehicleId { get; set; }
        public string? InitialMessage { get; set; }
    }
}
