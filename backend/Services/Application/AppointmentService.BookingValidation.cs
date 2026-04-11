using System;
using System.Collections.Generic;
using MyGarage.DTOs.Requests;
using MyGarage.Constants;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private (DateTime startAt, DateTime endAt) ResolveRequestedWindow(
            BookingFlowType flow,
            BookAppointmentRequestDto dto)
        {
            if (flow == BookingFlowType.Flexible)
            {
                var startAt = dto.CheckInWindowStart ?? dto.StartTime
                    ?? throw new InvalidOperationException("C?n cung c?p khung gi? check-in b?t d?u.");
                var endAt = dto.CheckInWindowEnd ?? dto.EndTime ?? startAt;
                startAt = NormalizeRequestDateTimeToLocalClock(startAt);
                endAt = NormalizeRequestDateTimeToLocalClock(endAt);
                return (startAt, endAt);
            }

            var fixedStartAt = dto.StartTime
                ?? throw new InvalidOperationException("C?n cung c?p gi? b?t d?u cho l?ch c� gi?.");
            var fixedEndAt = dto.EndTime ?? fixedStartAt;
            fixedStartAt = NormalizeRequestDateTimeToLocalClock(fixedStartAt);
            fixedEndAt = NormalizeRequestDateTimeToLocalClock(fixedEndAt);
            return (fixedStartAt, fixedEndAt);
        }

        private static DateTime NormalizeRequestDateTimeToLocalClock(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value.ToLocalTime();
            if (value.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(value, DateTimeKind.Local);
            return value;
        }

        private void ValidateBookingBasics(
            BookAppointmentRequestDto dto,
            BookingFlowType flow,
            IReadOnlyCollection<long> requestedServiceIds,
            IReadOnlyCollection<long> requestedPackageIds,
            DateTime requestedStartAt)
        {
            if (requestedStartAt.Date < _clock.LocalNow.Date)
                throw new InvalidOperationException("Kh�ng th? d?t l?ch cho ng�y trong qu� kh?.");
            if (string.IsNullOrWhiteSpace(dto.CustomerFullName))
                throw new InvalidOperationException("Vui l�ng nh?p h? t�n kh�ch h�ng.");
            if (string.IsNullOrWhiteSpace(dto.CustomerPhone))
                throw new InvalidOperationException("Vui lòng nhập số điện thoại khách hàng.");
            if (flow == BookingFlowType.Fixed && requestedServiceIds.Count == 0 && requestedPackageIds.Count == 0)
                throw new InvalidOperationException("Luồng giờ cố định yêu cầu chọn ít nhất một dịch vụ cụ thể.");

            if (!dto.VehicleId.HasValue)
            {
                if (!dto.VehicleModelId.HasValue)
                    throw new InvalidOperationException("Vui l�ng ch?n d�ng xe.");
                if (string.IsNullOrWhiteSpace(dto.LicensePlate))
                    throw new InvalidOperationException("Vui lòng nhập biển số xe.");
                if (!dto.ManufactureYear.HasValue || dto.ManufactureYear.Value <= 0)
                    throw new InvalidOperationException("Vui lòng chọn năm sản xuất.");
            }
        }
    }
}
