using System;

namespace MyGarage.Application.Interfaces.Stores.Chatbot;

public interface IAppLogger<T>
{
    void LogDebug(string message, params object?[] args);
    void LogDebug(Exception exception, string message, params object?[] args);
    void LogInformation(string message, params object?[] args);
    void LogWarning(string message, params object?[] args);
    void LogWarning(Exception exception, string message, params object?[] args);
    void LogError(string message, params object?[] args);
    void LogError(Exception exception, string message, params object?[] args);
}
