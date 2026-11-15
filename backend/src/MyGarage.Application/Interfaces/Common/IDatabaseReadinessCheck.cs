namespace MyGarage.Application.Interfaces.Common;

public interface IDatabaseReadinessCheck
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
