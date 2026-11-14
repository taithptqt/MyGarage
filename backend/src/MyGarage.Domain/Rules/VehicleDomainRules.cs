using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyGarage.Domain.Rules;

public static class VehicleDomainRules
{
    public const string VietnamPlatePattern = @"^(?:TEMP-[A-Z0-9]{6,12}|\d{2}[A-Z0-9]{1,2}-?\d{3}(?:\.\d{1,2})?|\d{2}[A-Z0-9]{1,2}-?\d{4,5})$";
    public const string Vin17Pattern = @"^[A-HJ-NPR-Z0-9]{17}$";

    private static readonly Regex PlateRegex = new(VietnamPlatePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex VinRegex = new(Vin17Pattern, RegexOptions.Compiled);

    public static string? NormalizePlate(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate)) return null;
        return plate.Trim().ToUpperInvariant();
    }

    public static bool IsValidPlate(string? plate)
    {
        var normalized = NormalizePlate(plate);
        if (string.IsNullOrEmpty(normalized)) return false;
        return PlateRegex.IsMatch(normalized);
    }

    public static string? NormalizeVin(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin)) return null;
        return vin.Trim().ToUpperInvariant();
    }

    public static bool IsValidVin(string? vin)
    {
        var normalized = NormalizeVin(vin);
        if (string.IsNullOrEmpty(normalized)) return false;
        return VinRegex.IsMatch(normalized);
    }

    public static string GenerateTemporaryPlate()
    {
        return $"TEMP-{Guid.NewGuid():N}".ToUpperInvariant()[..12];
    }

    public static bool ValidateManufactureYear(int? year, int? currentYear = null)
    {
        if (!year.HasValue) return true; // Optional field
        var maxYear = (currentYear ?? DateTime.UtcNow.Year) + 1;
        return year.Value is >= 1900 and <= 2100 && year.Value <= maxYear;
    }

    public static bool ValidateOdo(int? newOdo, int? existingOdo = null)
    {
        if (!newOdo.HasValue) return true;
        if (newOdo.Value < 0) return false;
        if (existingOdo.HasValue && newOdo.Value < existingOdo.Value)
        {
            // Odometer rolling backwards is abnormal
            return false;
        }
        return true;
    }

    public static bool CanAccessVehicle(bool isStaff, long? customerId, IEnumerable<long>? ownedVehicleIds, long vehicleId)
    {
        if (isStaff) return true;
        if (!customerId.HasValue || customerId.Value <= 0) return false;
        if (vehicleId <= 0) return false;
        if (ownedVehicleIds == null) return false;

        return ownedVehicleIds.Contains(vehicleId);
    }

    public static bool CanAccessVehicle(bool isStaff, long? requestingCustomerId, long? vehicleOwnerCustomerId)
    {
        if (isStaff) return true;
        if (!requestingCustomerId.HasValue || requestingCustomerId.Value <= 0) return false;
        if (!vehicleOwnerCustomerId.HasValue || vehicleOwnerCustomerId.Value <= 0) return false;

        return requestingCustomerId.Value == vehicleOwnerCustomerId.Value;
    }

    public static VehicleOwnershipTransitionResult TransitionOwnership(
        long vehicleId,
        long? previousOwnerId,
        long? newOwnerId,
        DateOnly today,
        DateOnly? existingStartDate = null)
    {
        if (vehicleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(vehicleId), "Mã xe không hợp lệ.");

        if (previousOwnerId.HasValue && newOwnerId.HasValue && previousOwnerId.Value == newOwnerId.Value)
        {
            return new VehicleOwnershipTransitionResult(
                IsUnchanged: true,
                PreviousOwnerDeactivated: false,
                NewOwnerActivated: true,
                StartDate: existingStartDate ?? today,
                TransferDate: null
            );
        }

        var previousDeactivated = previousOwnerId.HasValue && previousOwnerId.Value > 0;
        var newActivated = newOwnerId.HasValue && newOwnerId.Value > 0;

        return new VehicleOwnershipTransitionResult(
            IsUnchanged: false,
            PreviousOwnerDeactivated: previousDeactivated,
            NewOwnerActivated: newActivated,
            StartDate: newActivated ? (existingStartDate ?? today) : null,
            TransferDate: previousDeactivated ? today : null
        );
    }
}

public sealed record VehicleOwnershipTransitionResult(
    bool IsUnchanged,
    bool PreviousOwnerDeactivated,
    bool NewOwnerActivated,
    DateOnly? StartDate,
    DateOnly? TransferDate
);
