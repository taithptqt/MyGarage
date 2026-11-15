namespace MyGarage.Interfaces
{
    public interface IDiagnosisQueryService :
        IDiagnosisDataQueryService,
        IDiagnosisAssignmentReader,
        IDiagnosisApprovalReader
    {
    }
}
