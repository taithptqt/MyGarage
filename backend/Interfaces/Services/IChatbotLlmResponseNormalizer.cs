namespace MyGarage.Interfaces;

public interface IChatbotLlmResponseNormalizer
{
    ChatbotLlmGatewayResponse Normalize(ChatbotLlmProviderRawResponse raw);
}
