using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public sealed record StaffTierInfo(long Id, long? StaffTierId);
public sealed record VehicleEngineFuelInfo(long ModelId, string? FuelType);
public sealed record AssignmentLockRow(DateTime? ActualStartTime, DateTime? ActualEndTime, string? Status);
public sealed record MaterialRequestLockRow(decimal? IssuedQuantity, string? DetailStatus, string? RequestStatus);
public sealed record GatePassLockRow(string? Status, DateTime? PassedAt);
public sealed record EditRequestAuditLogRow(long Id, string? NewValue);

public interface IServiceOrderStore :
    IServiceOrderCommandStore,
    IServiceOrderQueryStore,
    IServiceOrderIntakeContextStore,
    IServiceOrderCommissionStore
{
}
