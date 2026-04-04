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
                return (startAt, endAt);
            }

            var fixedStartAt = dto.StartTime
                ?? throw new InvalidOperationException("C?n cung c?p gi? b?t d?u cho l?ch có gi?.");
            var fixedEndAt = dto.EndTime ?? fixedStartAt;
            return (fixedStartAt, fixedEndAt);
        }

        private void ValidateBookingBasics(
            BookAppointmentRequestDto dto,
            BookingFlowType flow,
            IReadOnlyCollection<long> requestedServiceIds,
            DateTime requestedStartAt)
        {
            if (requestedStartAt.Date < _clock.LocalNow.Date)
                throw new InvalidOperationException("Không th? d?t l?ch cho ngày trong quá kh?.");
            if (string.IsNullOrWhiteSpace(dto.CustomerFullName))
                throw new InvalidOperationException("Vui lòng nh?p h? tên khách hàng.");
            if (string.IsNullOrWhiteSpace(dto.CustomerPhone))
                throw new InvalidOperationException("Vui lòng nh?p s? di?n tho?i khách hàng.");
            if (flow == BookingFlowType.Fixed && requestedServiceIds.Count == 0)
                throw new InvalidOperationException("Lu?ng gi? c? d?nh yêu c?u ch?n ít nh?t m?t d?ch v? c? th?.");

            if (!dto.VehicleId.HasValue)
            {
                if (!dto.VehicleModelId.HasValue)
                    throw new InvalidOperationException("Vui lòng ch?n dòng xe.");
                if (string.IsNullOrWhiteSpace(dto.LicensePlate))
                    throw new InvalidOperationException("Vui lòng nh?p bi?n s? xe.");
                if (string.IsNullOrWhiteSpace(dto.Vin))
                    throw new InvalidOperationException("Vui lòng nh?p s? VIN.");
            }
        }
    }
}
