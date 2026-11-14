namespace MyGarage.Application.Ai;

public sealed record DispatchBayCandidate(long Id, string Code, string Name);

public interface IDispatchBayCandidates
{
    Task<IReadOnlyList<DispatchBayCandidate>> GetAvailableAsync(long branchId, CancellationToken cancellationToken = default);
}
