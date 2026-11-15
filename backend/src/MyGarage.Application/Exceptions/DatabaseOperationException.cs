namespace MyGarage.Application.Exceptions;

public class DatabaseOperationException : Exception
{
    public DatabaseOperationException(string message) : base(message) { }

    public DatabaseOperationException(string message, Exception innerException) : base(message, innerException) { }
}
