using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private const string BayTypeQuickLift = "QS_LIFT";
        private const string BayTypeQuickFlat = "QS_FLAT";
        private const string BayTypeRepairLift = "GR_LIFT";
        private const string BayTypeRepairAlign = "GR_ALIGN";
        private const string BayTypeRepairElectrical = "GR_ELECTRICAL";
        private const string BayTypeSpecialDiagnose = "SP_DIAGNOSE";
        private const string BayTypeSpecialBodyPaint = "SP_BODY_PAINT";
        private const string BayTypeSpecialDetailing = "SP_DETAILING";

        private enum BookingBayArea
        {
            Diagnose = 1,
            Quick = 2,
            Repair = 3,
            BodyPaint = 4,
            Detailing = 5
        }

        private sealed record BaySelectionPolicy(
            bool IsDiagnoseOnly,
            ServiceCluster RequestedCluster,
            BookingBayArea PrimaryArea,
            IReadOnlyList<string> OrderedBayTypes);

        private async Task<BaySelectionPolicy> ResolveBaySelectionPolicyAsync(
            string? requestedServiceIds,
            ServiceCluster requestedCluster,
            string? requestedPackageIds = null)
        {
            var services = await LoadRequestedServicesWithPackageItemsAsync(requestedServiceIds, requestedPackageIds);
            var packages = await LoadRequestedPackagesAsync(requestedPackageIds);
            if (services.Count == 0 && packages.Count == 0)
            {
                return CreateDefaultBaySelectionPolicy(requestedCluster);
            }

            var isDiagnoseOnly = (services.Count > 0 || packages.Count > 0) &&
                services.All(IsInspectionService) &&
                packages.All(IsInspectionPackage);
            var minutesByArea = new Dictionary<BookingBayArea, int>
            {
                [BookingBayArea.Diagnose] = 0,
                [BookingBayArea.Quick] = 0,
                [BookingBayArea.Repair] = 0,
                [BookingBayArea.BodyPaint] = 0,
                [BookingBayArea.Detailing] = 0
            };

            var needsElectricalRepair = false;
            var needsAlignmentRepair = false;

            foreach (var service in services)
            {
                var area = ResolveServiceAreaBucket(service, requestedCluster);
                var minutes = Math.Max(15, service.StandardMinutes);
                minutesByArea[area] += minutes;

                needsElectricalRepair |= IsElectricalRepairSignal(service);
                needsAlignmentRepair |= IsAlignmentRepairSignal(service);
            }

            foreach (var package in packages)
            {
                var area = ResolvePackageAreaBucket(package, requestedCluster);
                var minutes = Math.Max(15, package.StandardMinutes);
                minutesByArea[area] += minutes;
            }

            var primaryArea = ResolvePrimaryAreaByDuration(
                minutesByArea,
                requestedCluster,
                isDiagnoseOnly);
            var orderedBayTypes = BuildOrderedBayTypes(
                primaryArea,
                isDiagnoseOnly,
                needsElectricalRepair,
                needsAlignmentRepair,
                requestedCluster);

            return new BaySelectionPolicy(
                IsDiagnoseOnly: isDiagnoseOnly,
                RequestedCluster: requestedCluster,
                PrimaryArea: primaryArea,
                OrderedBayTypes: orderedBayTypes);
        }

        private static BookingBayArea ResolvePackageAreaBucket(
            Package package,
            ServiceCluster requestedCluster)
        {
            if (IsInspectionPackage(package))
            {
                return BookingBayArea.Diagnose;
            }

            var categoryCode = NormalizeToken(package.Category?.Code);
            var text = NormalizeText($"{package.Code} {package.Name} {package.Category?.Name}");
            if (categoryCode is "INSURANCE_REPAIR" or "BODY_PAINT" ||
                text.Contains("bao hiem", StringComparison.Ordinal) ||
                text.Contains("dong son", StringComparison.Ordinal) ||
                text.Contains("son", StringComparison.Ordinal) ||
                text.Contains("tai nan", StringComparison.Ordinal))
            {
                return BookingBayArea.BodyPaint;
            }

            if (categoryCode is "DETAILING" or "CAR_CARE" ||
                text.Contains("cham soc", StringComparison.Ordinal) ||
                text.Contains("detailing", StringComparison.Ordinal))
            {
                return BookingBayArea.Detailing;
            }

            return requestedCluster == ServiceCluster.Heavy
                ? BookingBayArea.Repair
                : BookingBayArea.Quick;
        }

        private static BaySelectionPolicy CreateDefaultBaySelectionPolicy(ServiceCluster requestedCluster)
        {
            var primaryArea = requestedCluster == ServiceCluster.Heavy
                ? BookingBayArea.Repair
                : BookingBayArea.Quick;

            return new BaySelectionPolicy(
                IsDiagnoseOnly: false,
                RequestedCluster: requestedCluster,
                PrimaryArea: primaryArea,
                OrderedBayTypes: BuildOrderedBayTypes(
                    primaryArea,
                    isDiagnoseOnly: false,
                    needsElectricalRepair: false,
                    needsAlignmentRepair: false,
                    requestedCluster: requestedCluster));
        }

        private static BookingBayArea ResolveServiceAreaBucket(
            StandardService service,
            ServiceCluster requestedCluster)
        {
            if (IsInspectionService(service))
            {
                return BookingBayArea.Diagnose;
            }

            var categoryCode = NormalizeToken(service.Category?.Code);
            var categoryName = NormalizeText(service.Category?.Name ?? string.Empty);
            var code = NormalizeToken(service.Code);
            if (categoryCode is "BODY_PAINT" or "INSURANCE_REPAIR" ||
                code.StartsWith("DS_", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("dong son", StringComparison.Ordinal))
            {
                return BookingBayArea.BodyPaint;
            }

            if (categoryCode is "DETAILING" or "CAR_CARE" ||
                code.StartsWith("DT_", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("cham soc", StringComparison.Ordinal) ||
                categoryName.Contains("detailing", StringComparison.Ordinal))
            {
                return BookingBayArea.Detailing;
            }

            if (categoryCode is "QUICK_SERVICE" or "MAINTENANCE")
            {
                return BookingBayArea.Quick;
            }

            if (categoryCode is "GENERAL_REPAIR" or "REWORK" or "WARRANTLY" or "WARRANTY")
            {
                return BookingBayArea.Repair;
            }

            return requestedCluster == ServiceCluster.Heavy
                ? BookingBayArea.Repair
                : BookingBayArea.Quick;
        }

        private static bool IsElectricalRepairSignal(StandardService service)
        {
            var code = NormalizeToken(service.Code);
            var name = NormalizeText(service.Name ?? string.Empty);

            return code.StartsWith("SC_D", StringComparison.OrdinalIgnoreCase) ||
                   code.Contains("ELEC", StringComparison.OrdinalIgnoreCase) ||
                   code.Contains("EV_COM", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("dien", StringComparison.Ordinal) ||
                   name.Contains("dieu hoa", StringComparison.Ordinal) ||
                   name.Contains("may lanh", StringComparison.Ordinal);
        }

        private static bool IsAlignmentRepairSignal(StandardService service)
        {
            var code = NormalizeToken(service.Code);
            var name = NormalizeText(service.Name ?? string.Empty);

            return code.Contains("ALIGN", StringComparison.OrdinalIgnoreCase) ||
                   code.Contains("SC_G04", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("can chinh thuoc lai", StringComparison.Ordinal) ||
                   name.Contains("do chum", StringComparison.Ordinal);
        }

        private static BookingBayArea ResolvePrimaryAreaByDuration(
            IReadOnlyDictionary<BookingBayArea, int> minutesByArea,
            ServiceCluster requestedCluster,
            bool isDiagnoseOnly)
        {
            if (isDiagnoseOnly)
            {
                return BookingBayArea.Diagnose;
            }

            var ranked = minutesByArea
                .Where(x => x.Key != BookingBayArea.Diagnose && x.Value > 0)
                .OrderByDescending(x => x.Value)
                .ThenBy(x => ResolveAreaTieBreaker(x.Key, requestedCluster))
                .Select(x => x.Key)
                .ToList();

            if (ranked.Count > 0)
            {
                return ranked[0];
            }

            return requestedCluster == ServiceCluster.Heavy
                ? BookingBayArea.Repair
                : BookingBayArea.Quick;
        }

        private static int ResolveAreaTieBreaker(BookingBayArea area, ServiceCluster requestedCluster)
        {
            return area switch
            {
                BookingBayArea.BodyPaint => 0,
                BookingBayArea.Detailing => 1,
                BookingBayArea.Repair when requestedCluster == ServiceCluster.Heavy => 2,
                BookingBayArea.Quick when requestedCluster == ServiceCluster.Quick => 2,
                BookingBayArea.Repair => 3,
                BookingBayArea.Quick => 3,
                BookingBayArea.Diagnose => 4,
                _ => 5
            };
        }

        private static IReadOnlyList<string> BuildOrderedBayTypes(
            BookingBayArea primaryArea,
            bool isDiagnoseOnly,
            bool needsElectricalRepair,
            bool needsAlignmentRepair,
            ServiceCluster requestedCluster)
        {
            var ordered = new List<string>();

            if (isDiagnoseOnly)
            {
                AppendUnique(ordered, BayTypeSpecialDiagnose);
                AppendUnique(ordered, BayTypeQuickLift);
                AppendUnique(ordered, BayTypeQuickFlat);
                AppendUnique(ordered, BayTypeRepairLift);
                AppendUnique(ordered, BayTypeRepairAlign);
                AppendUnique(ordered, BayTypeRepairElectrical);
                return ordered;
            }

            switch (primaryArea)
            {
                case BookingBayArea.BodyPaint:
                    AppendUnique(ordered, BayTypeSpecialBodyPaint);
                    break;

                case BookingBayArea.Detailing:
                    AppendUnique(ordered, BayTypeSpecialDetailing);
                    break;

                case BookingBayArea.Repair:
                    if (needsElectricalRepair)
                    {
                        AppendUnique(ordered, BayTypeRepairElectrical);
                    }

                    if (needsAlignmentRepair)
                    {
                        AppendUnique(ordered, BayTypeRepairAlign);
                    }

                    AppendUnique(ordered, BayTypeRepairLift);
                    AppendUnique(ordered, BayTypeRepairAlign);
                    AppendUnique(ordered, BayTypeRepairElectrical);
                    break;

                case BookingBayArea.Diagnose:
                    AppendUnique(ordered, BayTypeSpecialDiagnose);
                    break;

                default:
                    AppendUnique(ordered, BayTypeQuickLift);
                    AppendUnique(ordered, BayTypeQuickFlat);
                    AppendUnique(ordered, BayTypeRepairLift);
                    break;
            }

            if (requestedCluster == ServiceCluster.Heavy)
            {
                AppendUnique(ordered, BayTypeRepairLift);
                AppendUnique(ordered, BayTypeRepairAlign);
                AppendUnique(ordered, BayTypeRepairElectrical);
                AppendUnique(ordered, BayTypeQuickLift);
                AppendUnique(ordered, BayTypeQuickFlat);
            }
            else
            {
                AppendUnique(ordered, BayTypeQuickLift);
                AppendUnique(ordered, BayTypeQuickFlat);
                AppendUnique(ordered, BayTypeRepairLift);
                AppendUnique(ordered, BayTypeRepairAlign);
                AppendUnique(ordered, BayTypeRepairElectrical);
            }

            AppendUnique(ordered, BayTypeSpecialBodyPaint);
            AppendUnique(ordered, BayTypeSpecialDetailing);
            AppendUnique(ordered, BayTypeSpecialDiagnose);
            return ordered;
        }

        private static void AppendUnique(List<string> items, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (items.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            items.Add(value);
        }

        private static string ResolveTargetBayAreaTag(BookingBayArea area)
        {
            return area switch
            {
                BookingBayArea.BodyPaint => "BODY_PAINT",
                BookingBayArea.Detailing => "DETAILING",
                BookingBayArea.Diagnose => "DIAGNOSE",
                BookingBayArea.Quick => "QUICK",
                _ => "REPAIR"
            };
        }
    }
}
