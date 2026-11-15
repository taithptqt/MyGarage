using System;
using Microsoft.Extensions.Logging;
using MyGarage.Application.Interfaces.Stores.Chatbot;

namespace MyGarage.Infrastructure.Persistence.Stores.Chatbot;

public sealed class AppLoggerAdapter<T> : IAppLogger<T>
{
    private readonly ILogger<T> _logger;

    public AppLoggerAdapter(ILogger<T> logger)
    {
        _logger = logger;
    }

    public void LogDebug(string message, params object?[] args) => _logger.LogDebug(message, args);

    public void LogDebug(Exception exception, string message, params object?[] args) => _logger.LogDebug(exception, message, args);

    public void LogInformation(string message, params object?[] args) => _logger.LogInformation(message, args);

    public void LogWarning(string message, params object?[] args) => _logger.LogWarning(message, args);

    public void LogWarning(Exception exception, string message, params object?[] args) => _logger.LogWarning(exception, message, args);

    public void LogError(string message, params object?[] args) => _logger.LogError(message, args);

    public void LogError(Exception exception, string message, params object?[] args) => _logger.LogError(exception, message, args);
}
