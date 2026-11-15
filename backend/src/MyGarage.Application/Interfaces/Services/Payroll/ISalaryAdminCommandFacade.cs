namespace MyGarage.Application.Payroll;

public interface ISalaryAdminCommandFacade
{
    Task<SalaryAdminCommandResult> GetSalaryProfilesAsync(
        long? branchId,
        int? page,
        int? pageSize,
        SalaryAdminActorContext actor,
        CancellationToken ct = default);

    Task<SalaryAdminCommandResult> CreateSalaryProfileAsync(
        SalaryProfileUpsertDto request,
        SalaryAdminActorContext actor,
        CancellationToken ct = default);

    Task<SalaryAdminCommandResult> UpdateSalaryProfileAsync(
        long id,
        SalaryProfileUpsertDto request,
        SalaryAdminActorContext actor,
        CancellationToken ct = default);

    Task<SalaryAdminCommandResult> DeleteSalaryProfileAsync(
        long id,
        SalaryAdminActorContext actor,
        CancellationToken ct = default);

    Task<SalaryAdminCommandResult> GetInitialSalaryRequestsAsync(
        string? status,
        long? branchId,
        int? page,
        int? pageSize,
        SalaryAdminActorContext actor,
        CancellationToken ct = default);

    Task<SalaryAdminCommandResult> ApproveInitialSalaryRequestAsync(
        long id,
        SalaryApprovalDecisionDto request,
        SalaryAdminActorContext actor,
        CancellationToken ct = default);

    Task<SalaryAdminCommandResult> RejectInitialSalaryRequestAsync(
        long id,
        SalaryApprovalDecisionDto request,
        SalaryAdminActorContext actor,
        CancellationToken ct = default);
}
