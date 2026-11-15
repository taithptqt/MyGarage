namespace MyGarage.Application.Payroll;

public sealed record SalaryProfileListResult(IReadOnlyList<Dictionary<string, object?>> Items, int? Total);

public interface ISalaryProfileListReader
{
    Task<SalaryProfileListResult> ReadAsync(long? branchId, int? page, int pageSize, CancellationToken cancellationToken);
}

public sealed class SalaryProfileListQuery(ISalaryProfileListReader reader)
{
    public Task<SalaryProfileListResult> ExecuteAsync(long? branchId, int? page, int pageSize, CancellationToken cancellationToken)
        => reader.ReadAsync(branchId, page, pageSize, cancellationToken);
}
