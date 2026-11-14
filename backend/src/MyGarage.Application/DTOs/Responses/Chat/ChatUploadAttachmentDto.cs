namespace MyGarage.DTOs.Responses.Chat
{
    public class ChatUploadAttachmentDto
    {
        public string AttachmentUrl { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long? FileSize { get; set; }
        public int SortOrder { get; set; }
    }
}
