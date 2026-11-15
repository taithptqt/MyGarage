using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ChatMessageService : GenericService<ChatMessage>, IChatMessageService
    {
        public ChatMessageService(IChatMessageRepository repository) : base(repository)
        {
        }
    }
}
