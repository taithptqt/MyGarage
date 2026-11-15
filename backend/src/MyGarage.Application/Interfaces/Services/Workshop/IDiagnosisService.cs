namespace MyGarage.Interfaces
{
    public interface IDiagnosisService :
        IDiagnosisCrudService,
        IDiagnosisQueryService,
        IDiagnosisIntakeService,
        IDiagnosisQueueService,
        IDiagnosisApprovalWorkflowService
    {
    }
}
