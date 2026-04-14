using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ConversationService : GenericService<Conversation>, IConversationService
    {
        public ConversationService(IConversationRepository repository) : base(repository)
        {
        }
    }
}
