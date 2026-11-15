using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public sealed record DiagnosisServiceOrderBoardRow(long Id, string? Code, long ExecuteBranchId, string? Status);
public sealed record DiagnosisDispatchServiceOrderInfo(long Id, long ExecuteBranchId, string? OrderCategory, string? Status);
public sealed record DiagnosisIntakeServiceOrderInfo(long Id, long ExecuteBranchId, long VehicleId, string? OrderCategory, string? CustomerNotes, DateTime? CheckInTime, DateTime? CreatedAt);
public sealed record DiagnosisQueueServiceOrderInfo(long Id, long ExecuteBranchId, long? CustomerId, long VehicleId, long? AppointmentId, DateTime? CheckInTime, DateTime? CreatedAt);
public sealed record DiagnosisSnapshotRow(long Id, long ServiceOrderId, int RevisionNumber, DateTime? CreatedAt, string? SnapshotData);
public sealed record DiagnosisServiceSignalRow(string? Code, string? Name);
public sealed record DiagnosisStandardServiceDurationRow(int StandardMinutes, decimal Quantity);
public sealed record DiagnosisPackageCandidateRow(long Id, string? Code, string? Name, int StandardMinutes);
public sealed record DiagnosisTeamProfileRow(string? SpecialtyTag, string? Code);
public sealed record DiagnosisDispatchTeamRow(long Id, string? Name, string? Code, string? SpecialtyTag, long? BranchId);
public sealed record DiagnosisTechnicianRow(long Id, string? FullName, int? SkillLevel);
public sealed record DiagnosisAssignmentLoadRow(long DiagnosisId, long ServiceOrderId, long? TechnicianId, long? AssistantTechnicianId, DateTime? CreatedAt, long Id);
public sealed record DiagnosisTeamLoadRow(long DiagnosisId, long ServiceOrderId, long TeamId, DateTime? CreatedAt, long Id);
public sealed record DiagnosisBayRow(long Id, string? Code, string? Name, string? BayType, long? CurrentOccupiedVehicleId, string? OperationalStatus);
public sealed record DiagnosisBayIdCodeNameRow(long Id, string? Code, string? Name);
public sealed record DiagnosisBayDetailRow(long Id, string? Code, string? Name, long? BranchId, string? BayType);
public sealed record DiagnosisBayAvailabilityRow(string? BayType, long? CurrentOccupiedVehicleId, string? OperationalStatus);
public sealed record DiagnosisBlockingWorkSlotRow(long BayId, DateTime StartTime, DateTime EndTime);
public sealed record DiagnosisNextBookingRow(long Id, DateTime StartTime);

public interface IDiagnosisStore :
    IDiagnosisCommandStore,
    IDiagnosisQueryStore,
    IDiagnosisOrderContextStore,
    IDiagnosisDispatchResourceStore
{
}
