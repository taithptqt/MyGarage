namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IDiagnosisQueryStore
    : IDiagnosisAssignmentQueryStore,
      IDiagnosisRecordQueryStore,
      IDiagnosisSnapshotSignalQueryStore
{
}
