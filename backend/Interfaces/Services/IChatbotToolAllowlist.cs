namespace MyGarage.Interfaces;

public interface IChatbotToolAllowlist
{
    bool IsAllowed(string? toolName);

    bool IsAllowedForLlm(string? toolName);

    bool IsWriteCapable(string? toolName);
}
