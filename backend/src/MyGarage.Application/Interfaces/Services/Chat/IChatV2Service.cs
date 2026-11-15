namespace MyGarage.Interfaces
{
    public interface IChatV2Service :
        IChatV2CustomerConversationService,
        IChatV2CustomerMessagingService,
        IChatV2ReceptionConversationService,
        IChatV2ReceptionMessagingService
    {
    }
}
