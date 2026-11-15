using System;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisAssignmentReader
    {
        long? ReadDiagnosisAssignmentId(Diagnosis diagnosis);
        long? ReadAssignedTeamId(Diagnosis diagnosis);
        long? ReadTechnicianId(Diagnosis diagnosis);
        long? ReadAssistantTechnicianId(Diagnosis diagnosis);
        long? ReadAssignedBayId(Diagnosis diagnosis);
        DateTime? ReadCancellationRequestedAt(Diagnosis diagnosis);
        DateTime? ReadCancellationDecisionAt(Diagnosis diagnosis);
    }
}
