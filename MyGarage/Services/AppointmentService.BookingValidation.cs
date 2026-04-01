using System;
using System.Collections.Generic;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;

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
                    ?? throw new InvalidOperationException("Cần cung cấp khung giờ check-in bắt đầu.");
                var endAt = dto.CheckInWindowEnd ?? dto.EndTime
                    ?? throw new InvalidOperationException("Cần cung cấp khung giờ check-in kết thúc.");
                return (startAt, endAt);
            }

            var fixedStartAt = dto.StartTime
                ?? throw new InvalidOperationException("Cần cung cấp giờ bắt đầu cho lịch có giờ.");
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
                throw new InvalidOperationException("Không thể đặt lịch cho ngày trong quá khứ.");
            if (string.IsNullOrWhiteSpace(dto.CustomerFullName))
                throw new InvalidOperationException("Vui lòng nhập họ tên khách hàng.");
            if (string.IsNullOrWhiteSpace(dto.CustomerPhone))
                throw new InvalidOperationException("Vui lòng nhập số điện thoại khách hàng.");
            if (flow == BookingFlowType.Fixed && requestedServiceIds.Count == 0)
                throw new InvalidOperationException("Luồng giờ cố định yêu cầu chọn ít nhất một dịch vụ cụ thể.");
            if (flow == BookingFlowType.Flexible && requestedServiceIds.Count > 0)
                throw new InvalidOperationException("Check-in linh hoạt không nhận danh sách dịch vụ cố định.");

            if (!dto.VehicleId.HasValue)
            {
                if (!dto.VehicleModelId.HasValue)
                    throw new InvalidOperationException("Vui lòng chọn dòng xe.");
                if (string.IsNullOrWhiteSpace(dto.LicensePlate))
                    throw new InvalidOperationException("Vui lòng nhập biển số xe.");
                if (string.IsNullOrWhiteSpace(dto.Vin))
                    throw new InvalidOperationException("Vui lòng nhập số VIN.");
            }
        }
    }
}
