namespace MyGarage.Services;

// Expected, client-correctable authentication input or account conflicts.
public sealed class AuthRequestException(string message) : InvalidOperationException(message);
