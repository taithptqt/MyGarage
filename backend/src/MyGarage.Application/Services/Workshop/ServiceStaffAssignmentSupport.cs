using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    internal static class ServiceStaffAssignmentSupport
    {
        internal static readonly HashSet<string> AllowedStatuses =
            new(Enum.GetNames<ServiceProgressStatus>(), StringComparer.OrdinalIgnoreCase);

        internal static string? NormalizeAndValidateStatus(ServiceStaffAssignmentCreateDto dto, bool isCreate)
        {
            if (dto == null)
            {
                return "Dữ liệu phân công kỹ thuật không hợp lệ.";
            }

            var error = NormalizeAndValidateStatusValue(dto.Status, isCreate, out var updatedStatus);
            if (error == null && updatedStatus != null)
            {
                dto.Status = updatedStatus;
            }
            return error;
        }

        internal static string? NormalizeAndValidateStatus(ServiceStaffAssignmentUpdateDto dto, bool isCreate)
        {
            if (dto == null)
            {
                return "Dữ liệu phân công kỹ thuật không hợp lệ.";
            }

            var error = NormalizeAndValidateStatusValue(dto.Status, isCreate, out var updatedStatus);
            if (error == null && updatedStatus != null)
            {
                dto.Status = updatedStatus;
            }
            return error;
        }

        private static string? NormalizeAndValidateStatusValue(string? rawStatus, bool isCreate, out string? updatedStatus)
        {
            updatedStatus = null;
            var normalized = NormalizeStatus(rawStatus);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                if (isCreate)
                {
                    updatedStatus = nameof(ServiceProgressStatus.PENDING);
                }
                return null;
            }

            if (!AllowedStatuses.Contains(normalized))
            {
                var allowed = string.Join(", ", AllowedStatuses.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                return $"Giá trị status không hợp lệ: {rawStatus}. Chấp nhận: {allowed}.";
            }

            updatedStatus = normalized;
            return null;
        }

        internal static string NormalizeStatus(string? status)
            => string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToUpperInvariant();

        internal static string? NormalizeProofImageUrl(string? value)
        {
            var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return null;
            }

            return trimmed.Length <= 500 ? trimmed : trimmed[..500];
        }

        internal static string? ValidateInsuranceProofOnComplete(string? status, bool requiresInsuranceProof, string? proofImageUrl)
        {
            if (!requiresInsuranceProof)
            {
                return null;
            }

            if (!string.Equals(NormalizeStatus(status), nameof(ServiceProgressStatus.COMPLETED), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(proofImageUrl)
                ? "Hạng mục bảo hiểm bắt buộc ảnh nghiệm thu trước khi hoàn thành."
                : null;
        }

        internal static async Task<string?> ValidateSignedCheckInBeforeAssignmentAsync(
            IServiceStaffAssignmentQueryDataService dataService,
            IVehicleConditionLogValidationService vehicleConditionLogService,
            long serviceOrderDetailId)
        {
            if (serviceOrderDetailId <= 0)
            {
                return "Thiếu ServiceOrderDetailId để phân công kỹ thuật viên.";
            }

            var serviceOrderId = await dataService.GetServiceOrderIdFromDetailIdAsync(serviceOrderDetailId);
            if (serviceOrderId <= 0)
            {
                return "Không tìm thấy hạng mục dịch vụ của lệnh sửa chữa.";
            }

            var hasSignedCheckIn = await vehicleConditionLogService.HasSignedCheckInAsync(serviceOrderId);
            if (!hasSignedCheckIn)
            {
                return "Xe chưa hoàn tất check-in có chữ ký khách hàng. Vui lòng vào Nhật ký xe để hoàn tất trước khi giao việc cho thợ.";
            }

            return null;
        }

        internal static async Task<long> ResolveServiceOrderIdFromDetailIdAsync(
            IServiceStaffAssignmentQueryDataService dataService,
            long serviceOrderDetailId)
        {
            if (serviceOrderDetailId <= 0)
            {
                return 0;
            }

            return await dataService.GetServiceOrderIdFromDetailIdAsync(serviceOrderDetailId);
        }

        internal static async Task<bool> ResolveRequiresInsuranceProofAsync(
            IServiceStaffAssignmentQueryDataService dataService,
            long serviceOrderDetailId,
            bool explicitFlag)
        {
            if (explicitFlag)
            {
                return true;
            }

            if (serviceOrderDetailId <= 0)
            {
                return false;
            }

            var payerType = await dataService.GetDetailPayerTypeAsync(serviceOrderDetailId);
            return string.Equals(payerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(payerType, "SPLIT", StringComparison.OrdinalIgnoreCase);
        }

        internal static string ResolveRequiredSpecialty(string? itemName, string? description)
        {
            var source = CompactSearchText($"{itemName} {description}");
            if (ContainsAny(source, "SON", "DONG", "VO", "PAINT", "BODY"))
            {
                return "PAINTING";
            }

            if (ContainsAny(source, "DIEN", "ELECTRIC", "ECU", "CAMBIEN", "SENSOR", "MAYLANH", "AC"))
            {
                return "ELECTRICAL";
            }

            if (ContainsAny(source, "GAM", "CHASSIS", "PHANH", "TREO", "LAI", "BANH", "LOP"))
            {
                return "CHASSIS";
            }

            if (ContainsAny(source, "MAY", "ENGINE", "DONGCO", "XECMANG", "HOPSO"))
            {
                return "ENGINE";
            }

            return "GENERAL";
        }

        internal static bool TechnicianSupportsSpecialty(string? technicianSpecialty, string requiredSpecialty)
        {
            if (string.IsNullOrWhiteSpace(requiredSpecialty) ||
                string.Equals(requiredSpecialty, "GENERAL", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var source = CompactSearchText(technicianSpecialty);
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            if (ContainsAny(source, "GENERAL", "DANANG", "UNIVERSAL", "ALL"))
            {
                return true;
            }

            var required = CompactSearchText(requiredSpecialty);
            return source.Contains(required, StringComparison.Ordinal);
        }

        private static string CompactSearchText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToUpperInvariant(character));
                }
            }

            return builder
                .ToString()
                .Replace('Đ', 'D')
                .Replace('đ', 'D')
                .Normalize(NormalizationForm.FormC);
        }

        private static bool ContainsAny(string source, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            foreach (var token in tokens)
            {
                var normalized = CompactSearchText(token);
                if (source.Contains(normalized, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
