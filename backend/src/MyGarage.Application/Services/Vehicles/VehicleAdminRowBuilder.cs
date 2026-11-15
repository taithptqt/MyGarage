using MyGarage.Application.Interfaces.Queries;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class VehicleAdminRowBuilder
{
    internal static async Task<List<VehicleViewRow>> BuildVehicleRowsAsync(
        IVehicleAdminDataService dataService,
        string contentRootPath,
        bool includeHidden,
        VehicleAdminSettings? settings = null,
        BaySettingsSnapshot? baySettings = null,
        DateTime? nowUtc = null,
        CancellationToken ct = default)
    {
        settings ??= await VehicleAdminSupport.ReadSettingsAsync(contentRootPath);
        baySettings ??= await VehicleAdminSupport.ReadBaySettingsAsync(contentRootPath);
        var now = nowUtc ?? VietnamTime.Now;

        var activeSessionBayIds = baySettings.Sessions
            .Where(s => s.ReleasedAt == null)
            .Select(s => s.BayId)
            .Distinct()
            .ToList();

        var sourceData = await dataService.GetVehicleRowSourceDataAsync(includeHidden, activeSessionBayIds, ct);
        var vehicles = sourceData.Vehicles;
        if (vehicles.Count == 0) return new List<VehicleViewRow>();

        var vehicleIds = vehicles.Select(v => v.Id).ToList();

        var ownerByVehicle = sourceData.OwnerLinks
            .GroupBy(cv => cv.VehicleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsCurrentOwner == true)
                    .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                    .First());

        var latestOrderByVehicle = sourceData.Orders
            .GroupBy(o => o.VehicleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CheckInTime ?? x.CreatedAt ?? DateTime.MinValue).First());

        var activeSlotByVehicle = sourceData.WorkSlots
            .Where(w => w.VehicleId.HasValue && (w.ActualReleaseTime == null || VehicleAdminSupport.NormalizeForSearch(w.Status) == VehicleAdminSupport.StatusInProgress))
            .GroupBy(w => w.VehicleId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ActualStartTime ?? x.StartTime).First());

        var activeSessionsByVehicle = baySettings.Sessions
            .Where(s => s.ReleasedAt == null && vehicleIds.Contains(s.VehicleId))
            .GroupBy(s => s.VehicleId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AssignedAt).First());

        var fallbackBayByVehicle = sourceData.FallbackOccupiedBays
            .GroupBy(b => b.CurrentOccupiedVehicleId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue).First());

        var bays = sourceData.BaysByIds;
        var bayById = bays.ToDictionary(x => x.Id, x => x);

        var metadataStaffIds = settings.VehicleMetadata
            .Where(kvp => vehicleIds.Contains(kvp.Key) && kvp.Value?.CurrentStaffId.HasValue == true)
            .Select(kvp => kvp.Value!.CurrentStaffId!.Value)
            .Distinct()
            .ToList();

        var slotStaffIds = activeSlotByVehicle.Values
            .Where(x => x.TechnicianId.HasValue)
            .Select(x => x.TechnicianId!.Value)
            .Distinct()
            .ToList();

        var staffIds = metadataStaffIds.Concat(slotStaffIds).Distinct().ToList();
        var staffMap = await dataService.GetStaffMapAsync(staffIds, ct);

        var branchIds = settings.VehicleMetadata
            .Where(kvp => vehicleIds.Contains(kvp.Key) && kvp.Value?.AssignedBranchId.HasValue == true)
            .Select(kvp => kvp.Value!.AssignedBranchId!.Value)
            .Concat(latestOrderByVehicle.Values.Select(x => x.ExecuteBranchId))
            .Concat(bays.Select(b => b.BranchId))
            .Distinct()
            .ToList();

        var branchMap = await dataService.GetBranchMapAsync(branchIds, ct);

        var rows = new List<VehicleViewRow>(vehicles.Count);
        foreach (var vehicle in vehicles)
        {
            var metadata = VehicleAdminSupport.ResolveVehicleMetadata(settings, vehicle.Id);
            ownerByVehicle.TryGetValue(vehicle.Id, out var owner);
            latestOrderByVehicle.TryGetValue(vehicle.Id, out var latestOrder);
            activeSlotByVehicle.TryGetValue(vehicle.Id, out var activeSlot);

            Bay? currentBay = null;
            DateTime? bayAssignedAt = null;
            if (activeSessionsByVehicle.TryGetValue(vehicle.Id, out var activeSession))
            {
                bayAssignedAt = activeSession.AssignedAt;
                bayById.TryGetValue(activeSession.BayId, out currentBay);
            }
            else if (fallbackBayByVehicle.TryGetValue(vehicle.Id, out var fallbackBay))
            {
                currentBay = fallbackBay;
                bayAssignedAt = fallbackBay.UpdatedAt ?? fallbackBay.CreatedAt;
            }

            var status = VehicleAdminSupport.NormalizeVehicleStatus(metadata.CurrentStatus);
            if (string.IsNullOrWhiteSpace(status)) status = VehicleAdminSupport.NormalizeVehicleStatus(latestOrder?.Status);
            if (string.IsNullOrWhiteSpace(status)) status = VehicleAdminSupport.StatusWaitingReceive;

            var statusSince = metadata.StatusUpdatedAt
                ?? latestOrder?.CheckInTime
                ?? latestOrder?.CreatedAt
                ?? vehicle.UpdatedAt
                ?? vehicle.CreatedAt
                ?? now;

            var branchId = metadata.AssignedBranchId
                ?? latestOrder?.ExecuteBranchId
                ?? currentBay?.BranchId;

            string? branchName = null;
            if (branchId.HasValue && branchMap.TryGetValue(branchId.Value, out var branch))
            {
                branchName = branch.Name;
            }

            var staffId = metadata.CurrentStaffId ?? activeSlot?.TechnicianId;
            string? staffName = null;
            if (staffId.HasValue && staffMap.TryGetValue(staffId.Value, out var staff))
            {
                staffName = staff.FullName;
            }

            var checkInTime = latestOrder?.CheckInTime ?? latestOrder?.CreatedAt;
            var isHidden = vehicle.IsDeleted == true || metadata.IsHidden;
            var inShopHours = checkInTime.HasValue ? Math.Max(0, (now - checkInTime.Value).TotalHours) : 0;
            var statusHours = Math.Max(0, (now - statusSince).TotalHours);
            var bayHours = bayAssignedAt.HasValue ? Math.Max(0, (now - bayAssignedAt.Value).TotalHours) : 0;

            rows.Add(new VehicleViewRow
            {
                Id = vehicle.Id,
                VehicleCode = VehicleAdminSupport.FormatVehicleCode(vehicle.Id),
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                ModelId = vehicle.ModelId,
                ModelName = vehicle.Model?.ModelName ?? "-",
                BrandId = vehicle.Model?.BrandId ?? 0,
                BrandName = vehicle.Model?.Brand?.Name ?? "-",
                Segment = vehicle.Model?.Segment,
                Color = vehicle.Color,
                ManufactureYear = vehicle.ManufactureYear,
                Odo = vehicle.Odo,
                BranchId = branchId,
                BranchName = branchName,
                CheckInTime = checkInTime,
                CurrentStatus = status,
                StatusSince = statusSince,
                CurrentBayId = currentBay?.Id,
                CurrentBayCode = currentBay?.Code,
                CurrentBayName = currentBay?.Name,
                CurrentBayArea = currentBay == null ? null : VehicleAdminSupport.ResolveBayMetadata(baySettings, currentBay.Id).Area,
                CurrentStaffId = staffId,
                CurrentStaffName = staffName,
                CustomerId = owner?.CustomerId,
                CustomerName = owner?.Customer?.FullName,
                CustomerPhone = owner?.Customer?.Phone,
                TechnicalTeam = metadata.TechnicalTeam,
                ExpectedArea = metadata.ExpectedArea,
                Version = metadata.Version,
                EngineNumber = metadata.EngineNumber,
                ChassisNumber = metadata.ChassisNumber,
                InitialCondition = metadata.InitialCondition,
                IsEditLocked = metadata.IsEditLocked,
                IsHidden = isHidden,
                InShopHours = inShopHours,
                StatusDurationHours = statusHours,
                BayOccupiedHours = bayHours,
                UpdatedAt = MaxDate(vehicle.UpdatedAt, metadata.UpdatedAt, now)
            });
        }

        return rows;
    }

    internal static object MapVehicleListItem(VehicleViewRow row)
    {
        return new
        {
            id = row.Id,
            vehicleCode = row.VehicleCode,
            licensePlate = row.LicensePlate,
            vin = row.Vin,
            modelId = row.ModelId,
            modelName = row.ModelName,
            brandId = row.BrandId,
            brandName = row.BrandName,
            segment = row.Segment,
            color = row.Color,
            manufactureYear = row.ManufactureYear,
            odo = row.Odo,
            branchId = row.BranchId,
            branchName = row.BranchName,
            checkInTime = row.CheckInTime,
            currentStatus = row.CurrentStatus,
            statusSince = row.StatusSince,
            currentBayId = row.CurrentBayId,
            currentBayCode = row.CurrentBayCode,
            currentBayName = row.CurrentBayName,
            currentBayArea = row.CurrentBayArea,
            currentStaffId = row.CurrentStaffId,
            currentStaffName = row.CurrentStaffName,
            customerId = row.CustomerId,
            customerName = row.CustomerName,
            customerPhone = row.CustomerPhone,
            technicalTeam = row.TechnicalTeam,
            expectedArea = row.ExpectedArea,
            version = row.Version,
            engineNumber = row.EngineNumber,
            chassisNumber = row.ChassisNumber,
            initialCondition = row.InitialCondition,
            isEditLocked = row.IsEditLocked,
            isHidden = row.IsHidden,
            inShopHours = Math.Round(row.InShopHours, 2),
            statusDurationHours = Math.Round(row.StatusDurationHours, 2),
            bayOccupiedHours = Math.Round(row.BayOccupiedHours, 2),
            updatedAt = row.UpdatedAt
        };
    }

    private static DateTime MaxDate(DateTime? first, DateTime? second, DateTime fallback)
    {
        var a = first ?? DateTime.MinValue;
        var b = second ?? DateTime.MinValue;
        var max = a > b ? a : b;
        return max == DateTime.MinValue ? fallback : max;
    }
}
