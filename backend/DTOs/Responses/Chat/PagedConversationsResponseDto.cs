using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Chat
{
    public class PagedConversationsResponseDto
    {
        public IReadOnlyList<ConversationListItemDto> Items { get; set; } = [];
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
