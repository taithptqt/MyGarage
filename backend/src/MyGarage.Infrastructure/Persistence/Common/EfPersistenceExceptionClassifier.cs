using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Common;

namespace MyGarage.Infrastructure.Persistence.Common;

public sealed class EfPersistenceExceptionClassifier : IPersistenceExceptionClassifier
{
    public PersistenceExceptionClassification Classify(Exception exception)
    {
        return exception switch
        {
            DbUpdateConcurrencyException => new PersistenceExceptionClassification(
                PersistenceExceptionKind.ConcurrencyConflict),
            DbUpdateException updateEx => new PersistenceExceptionClassification(
                PersistenceExceptionKind.UpdateError,
                updateEx.InnerException?.Message ?? updateEx.Message),
            _ => new PersistenceExceptionClassification(PersistenceExceptionKind.None)
        };
    }
}
