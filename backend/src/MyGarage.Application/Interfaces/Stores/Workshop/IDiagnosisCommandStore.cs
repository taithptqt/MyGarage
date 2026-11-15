using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IDiagnosisCommandStore
{
    Task SaveChangesAsync();
    Task SaveChangesInTransactionAsync();
    Task ExecuteInTransactionAsync(Func<Task> action);

    void AddDiagnoses(IEnumerable<Diagnosis> items);
    void UpdateDiagnoses(IEnumerable<Diagnosis> items);
    void UpdateDiagnosis(Diagnosis item);
    void RemoveDiagnoses(IEnumerable<Diagnosis> items);
    void AddDiagnosisAssignments(IEnumerable<DiagnosisAssignment> items);
    void AddDiagnosisRecommendation(DiagnosisRecommendation item);
    void RemoveDiagnosisRecommendations(IEnumerable<DiagnosisRecommendation> items);
    void UpdateServiceOrder(ServiceOrder serviceOrder);
    void AddWorkSlot(WorkSlot slot);
    void UpdateWorkSlot(WorkSlot slot);
    void AddServiceOrderSnapshot(ServiceOrderSnapshot snapshot);
    void UpdateServiceOrderSnapshot(ServiceOrderSnapshot snapshot);
}
