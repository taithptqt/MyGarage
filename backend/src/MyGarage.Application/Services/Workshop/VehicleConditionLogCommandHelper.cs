using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    internal static class VehicleConditionLogCommandHelper
    {
        internal const string LogTypeCheckIn = "CHECK_IN";
        internal const string LogTypeCheckOut = "CHECK_OUT";

        internal static string NormalizeLogType(string? logType)
        {
            return (logType ?? string.Empty).Trim().ToUpperInvariant();
        }

        internal static async Task TryMarkServiceOrderCheckedInAsync(
            IVehicleConditionLogCommandService vehicleConditionLogService,
            string? logType,
            long serviceOrderId,
            string? customerSignatureUrl)
        {
            if (NormalizeLogType(logType) != LogTypeCheckIn)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(customerSignatureUrl))
            {
                return;
            }

            await vehicleConditionLogService.MarkServiceOrderCheckedInAsync(serviceOrderId);
        }

        internal static async Task TryAutoCreateDiagnosisQueueAfterCheckInAsync(
            IDiagnosisService diagnosisService,
            IVehicleConditionLogQueryService queryService,
            string? logType,
            long serviceOrderId,
            string? customerSignatureUrl,
            CancellationToken cancellationToken)
        {
            if (NormalizeLogType(logType) != LogTypeCheckIn)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(customerSignatureUrl))
            {
                return;
            }

            try
            {
                var serviceOrder = await diagnosisService.GetServiceOrderAsync(serviceOrderId);
                if (serviceOrder == null)
                {
                    return;
                }

                var shouldAutoCreateDiagnosis = await ShouldAutoCreateDiagnosisQueueAsync(
                    queryService,
                    serviceOrder,
                    cancellationToken);
                if (!shouldAutoCreateDiagnosis)
                {
                    return;
                }

                var hasSignedCheckIn = await diagnosisService.HasSignedCheckInAsync(serviceOrderId);
                if (!hasSignedCheckIn)
                {
                    return;
                }

                var existingDiagnoses = await diagnosisService.GetAllByServiceOrderAsync(serviceOrderId);
                if (existingDiagnoses.Count > 0)
                {
                    await diagnosisService.UpdateServiceOrderStatusAsync(
                        serviceOrder,
                        nameof(ServiceOrderLifecycleStatus.DIAGNOSING));
                    return;
                }

                var intakeSuggestion = await diagnosisService.BuildIntakeSuggestionAsync(
                    serviceOrderId,
                    dispatchTag: null,
                    bufferMinutesOverride: null,
                    requestedPackageCode: null);

                var suggestedTeamId = intakeSuggestion.SuggestedTeamId;
                if (!suggestedTeamId.HasValue || suggestedTeamId.Value <= 0)
                {
                    return;
                }

                var symptoms = ExtractSymptomsFromNotes(serviceOrder.CustomerNotes);
                var resolvedDiagnoseBayId = await diagnosisService.ResolveDiagnoseBayIdForBranchAsync(
                    serviceOrder.ExecuteBranchId,
                    intakeSuggestion.SuggestedBayId);
                var scheduledSlot = await diagnosisService.ScheduleDiagnosisQueueSlotAsync(
                    serviceOrderId,
                    intakeSuggestion.PackageEffectiveMinutes,
                    bufferMinutes: 15,
                    preferredBayId: resolvedDiagnoseBayId);
                var finalAssignedBayId = scheduledSlot?.BayId ?? resolvedDiagnoseBayId;
                await diagnosisService.CreatePendingDiagnosesAsync(
                    serviceOrderId,
                    technicianId: null,
                    symptoms,
                    intakeSuggestion.DispatchTag,
                    assignedTeamId: suggestedTeamId.Value,
                    assignedBayId: finalAssignedBayId);

                await diagnosisService.UpdateServiceOrderStatusAsync(
                    serviceOrder,
                    nameof(ServiceOrderLifecycleStatus.DIAGNOSING));
            }
            catch
            {
                // Keep check-in flow stable even if auto diagnosis bootstrap cannot be created.
            }
        }

        internal static string NormalizeSearchText(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('\u0111', 'd')
                .Replace('\u0110', 'd');
        }

        private static async Task<bool> ShouldAutoCreateDiagnosisQueueAsync(
            IVehicleConditionLogQueryService queryService,
            ServiceOrder serviceOrder,
            CancellationToken cancellationToken)
        {
            var requestedServiceIds = await queryService.LoadRequestedServiceIdsFromServiceOrderAsync(serviceOrder.Id, cancellationToken);
            if (requestedServiceIds.Count == 0 && serviceOrder.AppointmentId.HasValue)
            {
                requestedServiceIds = await LoadRequestedServiceIdsFromAppointmentAsync(
                    queryService,
                    serviceOrder.AppointmentId.Value,
                    cancellationToken);
            }

            if (requestedServiceIds.Count == 0)
            {
                return await HasInspectionKeywordInServiceOrderDetailsAsync(
                    queryService,
                    serviceOrder.Id,
                    cancellationToken);
            }

            var serviceRows = await queryService.GetInspectionServiceRowsAsync(requestedServiceIds, cancellationToken);
            return serviceRows.Any(row => IsInspectionService(row.Code, row.Name, row.CategoryCode, row.CategoryName));
        }

        private static async Task<bool> HasInspectionKeywordInServiceOrderDetailsAsync(
            IVehicleConditionLogQueryService queryService,
            long serviceOrderId,
            CancellationToken cancellationToken)
        {
            var detailRows = await queryService.GetServiceOrderDetailTextsAsync(serviceOrderId, cancellationToken);

            return detailRows.Any(row => IsInspectionService(
                serviceCode: null,
                serviceName: row.ItemName,
                categoryCode: null,
                categoryName: row.Description));
        }

        private static async Task<List<long>> LoadRequestedServiceIdsFromAppointmentAsync(
            IVehicleConditionLogQueryService queryService,
            long appointmentId,
            CancellationToken cancellationToken)
        {
            var requestedServiceIdsRaw = await queryService.GetAppointmentRequestedServiceIdsAsync(appointmentId, cancellationToken);
            return ParseRequestedServiceIds(requestedServiceIdsRaw);
        }

        private static List<long> ParseRequestedServiceIds(string? requestedServiceIds)
        {
            if (string.IsNullOrWhiteSpace(requestedServiceIds))
            {
                return new List<long>();
            }

            return requestedServiceIds
                .Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => long.TryParse(token.Trim(), out var parsedId) ? parsedId : 0L)
                .Where(parsedId => parsedId > 0)
                .Distinct()
                .ToList();
        }

        private static bool IsInspectionService(string? serviceCode, string? serviceName, string? categoryCode, string? categoryName)
        {
            _ = serviceName;
            _ = categoryCode;
            _ = categoryName;

            // Strict rule per business requirement: only 7 diagnosis package codes trigger auto diagnosis queue.
            var normalizedCode = NormalizeSearchText(serviceCode)
                .Replace("_", "-", StringComparison.Ordinal)
                .Trim()
                .ToUpperInvariant();

            return normalizedCode is "DG-MINI" or "DG-GEN" or "DG-SCAN" or "DG-ENG" or "DG-ELEC" or "DG-AC" or "DG-NOISE";
        }

        private static List<string> ExtractSymptomsFromNotes(string? customerNotes)
        {
            var fallback = new List<string>();
            if (string.IsNullOrWhiteSpace(customerNotes))
            {
                return fallback;
            }

            var notes = customerNotes.Trim();
            try
            {
                using var document = JsonDocument.Parse(notes);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    var values = root.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => (item.GetString() ?? string.Empty).Trim())
                        .Where(IsMeaningfulSymptom)
                        .Distinct()
                        .ToList();
                    return values.Count > 0 ? values : fallback;
                }

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("symptoms", out var symptomsElement) && symptomsElement.ValueKind == JsonValueKind.Array)
                    {
                        var values = symptomsElement.EnumerateArray()
                            .Where(item => item.ValueKind == JsonValueKind.String)
                            .Select(item => (item.GetString() ?? string.Empty).Trim())
                            .Where(IsMeaningfulSymptom)
                            .Distinct()
                            .ToList();
                        return values.Count > 0 ? values : fallback;
                    }

                    if (root.TryGetProperty("note", out var noteElement) && noteElement.ValueKind == JsonValueKind.String)
                    {
                        var note = (noteElement.GetString() ?? string.Empty).Trim();
                        return IsMeaningfulSymptom(note) ? new List<string> { note } : fallback;
                    }
                }
            }
            catch
            {
                // Legacy non-JSON format.
            }

            return IsMeaningfulSymptom(notes)
                ? new List<string> { notes }
                : fallback;
        }

        private static bool IsMeaningfulSymptom(string? text)
        {
            var value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = NormalizeSearchText(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (normalized == "khong co" ||
                normalized == "khong co ghi chu" ||
                normalized == "khong co ghi chu gi" ||
                normalized == "khong co mo ta")
            {
                return false;
            }

            if (normalized.StartsWith("[tiep nhan]", StringComparison.Ordinal) &&
                normalized.Contains("khong co", StringComparison.Ordinal))
            {
                return false;
            }

            if (normalized.StartsWith("[flow:", StringComparison.Ordinal) ||
                normalized.StartsWith("[cluster:", StringComparison.Ordinal) ||
                normalized.StartsWith("[dispatch", StringComparison.Ordinal) ||
                normalized.StartsWith("[auto_dispatch", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }
}
