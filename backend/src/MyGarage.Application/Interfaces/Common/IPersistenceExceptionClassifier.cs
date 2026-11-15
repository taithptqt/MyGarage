namespace MyGarage.Application.Interfaces.Common;

public enum PersistenceExceptionKind
{
    None = 0,
    ConcurrencyConflict = 1,
    UpdateError = 2
}

public readonly record struct PersistenceExceptionClassification(
    PersistenceExceptionKind Kind,
    string? DetailMessage = null);

public interface IPersistenceExceptionClassifier
{
    PersistenceExceptionClassification Classify(Exception exception);
}
