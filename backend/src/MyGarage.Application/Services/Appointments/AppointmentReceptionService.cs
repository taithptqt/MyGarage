using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentReceptionService : IAppointmentReceptionService
    {
        private readonly IAppointmentStore _store;
        private readonly ISystemClock _clock;
        private readonly IAppointmentBookingFlowResolver _bookingFlowResolver;
        private readonly IAppointmentLifecycleService _lifecycleService;
        private readonly IAppointmentQueryService _queryService;

        private const string ReceptionReplaceReason = "Điều chỉnh lịch hẹn bởi lễ tân";
        private const string ReceptionReplaceWindowPassedMessage = "Không thể sửa lịch hẹn vì xe đã đến xưởng.";

        public AppointmentReceptionService(
            IAppointmentStore store,
            ISystemClock clock,
            IAppointmentBookingFlowResolver bookingFlowResolver,
            IAppointmentLifecycleService lifecycleService,
            IAppointmentQueryService queryService)
        {
            _store = store;
            _clock = clock;
            _bookingFlowResolver = bookingFlowResolver;
            _lifecycleService = lifecycleService;
            _queryService = queryService;
        }

        public async Task ValidateReceptionSaveRequestAsync(ReceptionAppointmentSaveRequestDto dto)
        {
            if (dto == null) throw new InvalidOperationException("Dữ liệu đặt lịch không hợp lệ.");
            if (dto.BranchId <= 0) throw new InvalidOperationException("Chi nhánh hiện tại không hợp lệ.");

            if (dto.IsQuickOrder && dto.IsNewVehicle)
                throw new InvalidOperationException("Không thể đồng thời chọn khách vãng lai và xe mới của khách hiện có.");

            var plans = dto.VehiclePlans?.Where(plan => plan != null).ToList() ?? new List<ReceptionAppointmentVehiclePlanItemDto>();
            if (plans.Count == 0) throw new InvalidOperationException("Thiếu danh sách xe cần đặt lịch.");

            if ((dto.IsQuickOrder || dto.IsNewVehicle) && plans.Count > 1)
                throw new InvalidOperationException("Luồng khách vãng lai hoặc thêm xe mới chỉ hỗ trợ một xe mỗi lần lưu.");

            var replaceAppointmentId = dto.ReplaceAppointmentId.GetValueOrDefault();
            if (replaceAppointmentId > 0)
                await EnsureReceptionAppointmentStillEditableAsync(replaceAppointmentId);
        }

        public async Task<long> ResolveReceptionCustomerIdAsync(ReceptionAppointmentSaveRequestDto dto, long? actorStaffId)
        {
            if (dto.IsQuickOrder) return await ResolveOrCreateReceptionGuestCustomerAsync(dto.GuestName, dto.GuestPhone, actorStaffId);
            var customerId = dto.CustomerId.GetValueOrDefault();
            if (customerId <= 0) throw new InvalidOperationException("Khách hàng không hợp lệ.");
            var exists = await _store.ActiveCustomerExistsAsync(customerId);
            if (!exists) throw new InvalidOperationException("Không tìm thấy khách hàng đặt lịch.");
            return customerId;
        }

        public async Task<BookAppointmentRequestDto> BuildReceptionBookRequestAsync(
            ReceptionAppointmentSaveRequestDto dto,
            ReceptionAppointmentVehiclePlanItemDto plan,
            long customerId,
            long? actorStaffId)
        {
            if (!plan.StartTime.HasValue) throw new InvalidOperationException("Ngày giờ hẹn không hợp lệ.");
            var start = plan.StartTime.Value;
            var requestedServiceIds = NormalizeRequestedServiceIds(plan.RequestedServiceIds);
            var requestedPackageIds = NormalizeRequestedServiceIds(plan.RequestedPackageIds);
            var flow = await _bookingFlowResolver.ResolveFlowAsync(plan.BookingFlow, requestedServiceIds, requestedPackageIds);
            var end = plan.EndTime;
            var totalMinutes = plan.TotalMinutes;
            if (!end.HasValue && totalMinutes.HasValue && totalMinutes.Value > 0) end = start.AddMinutes(totalMinutes.Value);
            if (end.HasValue && end.Value < start) throw new InvalidOperationException("Ngày giờ hẹn không hợp lệ.");

            var checkInWindowStart = plan.CheckInWindowStart ?? start;
            var checkInWindowEnd = plan.CheckInWindowEnd ?? end ?? checkInWindowStart;
            if (checkInWindowEnd < checkInWindowStart) throw new InvalidOperationException("Khung giờ check-in không hợp lệ.");

            var customerNotes = NormalizeReceptionText(plan.CustomerNotes) ?? NormalizeReceptionText(dto.CustomerNotes);
            var booking = new BookAppointmentRequestDto
            {
                BranchId = dto.BranchId, CustomerId = customerId,
                CustomerFullName = dto.IsQuickOrder ? NormalizeReceptionText(dto.GuestName) : NormalizeReceptionText(dto.CustomerName),
                CustomerPhone = dto.IsQuickOrder ? NormalizeReceptionPhone(dto.GuestPhone) : NormalizeReceptionPhone(dto.CustomerPhone),
                BookingFlow = (flow == BookingFlowType.Flexible) ? "FLEXIBLE" : "FIXED",
                StartTime = start, EndTime = end ?? start,
                CheckInWindowStart = (flow == BookingFlowType.Flexible) ? checkInWindowStart : null,
                CheckInWindowEnd = (flow == BookingFlowType.Flexible) ? checkInWindowEnd : null,
                RequestedServiceIds = requestedServiceIds, RequestedPackageIds = requestedPackageIds,
                InsuranceCompanyId = (plan.InsuranceCompanyId ?? dto.InsuranceCompanyId) > 0 ? (plan.InsuranceCompanyId ?? dto.InsuranceCompanyId) : null,
                InsuranceDeductibleAmount = plan.InsuranceDeductibleAmount ?? dto.InsuranceDeductibleAmount,
                InsuranceDeductibleOption = NormalizeReceptionText(plan.InsuranceDeductibleOption ?? dto.InsuranceDeductibleOption),
                InsuranceClaimMode = NormalizeReceptionText(plan.InsuranceClaimMode ?? dto.InsuranceClaimMode),
                InsuranceQuoteState = NormalizeReceptionText(plan.InsuranceQuoteState ?? dto.InsuranceQuoteState),
                InsuranceCardImageUrls = plan.InsuranceCardImageUrls?.Count > 0 ? new List<string>(plan.InsuranceCardImageUrls) : new List<string>(dto.InsuranceCardImageUrls ?? new List<string>()),
                InsuranceDamageImageUrls = plan.InsuranceDamageImageUrls?.Count > 0 ? new List<string>(plan.InsuranceDamageImageUrls) : new List<string>(dto.InsuranceDamageImageUrls ?? new List<string>()),
                CustomerNotes = customerNotes, SlotHoldToken = NormalizeReceptionText(plan.SlotHoldToken), CreatedBy = actorStaffId
            };

            if (dto.IsQuickOrder) { booking.VehicleId = null; booking.VehicleModelId = plan.VehicleModelId ?? dto.GuestVehicleModelId; booking.LicensePlate = NormalizeReceptionPlate(plan.LicensePlate ?? dto.GuestVehiclePlate); booking.Vin = NormalizeReceptionText(plan.Vin ?? dto.GuestVehicleVin); booking.Color = NormalizeReceptionText(plan.Color ?? dto.GuestVehicleColor); booking.ManufactureYear = dto.GuestVehicleYear; }
            else if (dto.IsNewVehicle) { booking.VehicleId = null; booking.VehicleModelId = plan.VehicleModelId ?? dto.VehicleModelId; booking.LicensePlate = NormalizeReceptionPlate(plan.LicensePlate ?? dto.VehicleLicensePlate); booking.Vin = NormalizeReceptionText(plan.Vin ?? dto.VehicleVin); booking.Color = NormalizeReceptionText(plan.Color ?? dto.VehicleColor); booking.ManufactureYear = dto.VehicleYear; }
            else { var vehicleId = plan.VehicleId.GetValueOrDefault(); if (vehicleId <= 0) throw new InvalidOperationException("Vui lòng chọn xe của khách."); booking.VehicleId = vehicleId; }

            return booking;
        }

        public async Task CompleteReceptionReplacementAsync(
            long actorUserId,
            long replaceAppointmentId,
            IReadOnlyList<Appointment> createdAppointments)
        {
            if (replaceAppointmentId <= 0)
            {
                return;
            }

            if (createdAppointments.Count == 1)
            {
                await CopyAppointmentInsuranceAssetsAsync(replaceAppointmentId, createdAppointments[0].Id);
            }

            var canceled = await _lifecycleService.CancelAsync(replaceAppointmentId, ReceptionReplaceReason, actorUserId);
            if (canceled == null) throw new InvalidOperationException("Không tìm thấy lịch hẹn cần thay thế.");
        }

        public async Task<ReceptionAppointmentSaveResponseDto> BuildReceptionSaveResponseAsync(
            long customerId,
            long? groupSessionId,
            bool updateMode,
            IReadOnlyList<Appointment> createdAppointments)
        {
            var primary = createdAppointments[0];
            var customer = await _store.GetCustomerByIdAsync(customerId, asNoTracking: true);
            var vehicle = primary.VehicleId.HasValue
                ? await _store.GetVehicleByIdNoTrackingAsync(primary.VehicleId.Value)
                : null;

            return new ReceptionAppointmentSaveResponseDto
            {
                GroupSessionId = groupSessionId,
                CreatedCount = createdAppointments.Count,
                UpdateMode = updateMode,
                PrimaryAppointmentId = primary.Id,
                CustomerName = customer?.FullName,
                CustomerPhone = customer?.Phone,
                VehicleLicensePlate = vehicle?.LicensePlate,
                StartTime = primary.StartTime
            };
        }

        private async Task EnsureReceptionAppointmentStillEditableAsync(long appointmentId)
        {
            var capability = await _queryService.GetEditCapabilityAsync(appointmentId);
            if (capability == null) throw new InvalidOperationException("Không tìm thấy lịch hẹn cần thay thế.");
            if (!capability.CanEdit) throw new InvalidOperationException(capability.BlockingReason ?? ReceptionReplaceWindowPassedMessage);
        }

        private async Task<long> ResolveOrCreateReceptionGuestCustomerAsync(string? rawName, string? rawPhone, long? actorStaffId)
        {
            var guestName = NormalizeReceptionText(rawName);
            if (string.IsNullOrWhiteSpace(guestName)) throw new InvalidOperationException("Vui lòng nhập họ tên khách vãng lai.");
            var phone = NormalizeReceptionPhone(rawPhone);
            if (string.IsNullOrWhiteSpace(phone)) throw new InvalidOperationException("Vui lòng nhập số điện thoại khách vãng lai.");

            var existing = await _store.FindActiveCustomerByNormalizedPhoneAsync(phone);
            if (existing != null)
            {
                var changed = false;
                if (!string.Equals(existing.FullName, guestName, StringComparison.Ordinal)) { existing.FullName = guestName; changed = true; }
                if (!string.Equals(NormalizeReceptionPhone(existing.Phone), phone, StringComparison.Ordinal)) { existing.Phone = phone; changed = true; }
                if (existing.IsActive == false) { existing.IsActive = true; changed = true; }
                if (existing.IsDeleted == true) { existing.IsDeleted = false; existing.DeletedAt = null; existing.DeletedBy = null; changed = true; }
                if (changed) { existing.UpdatedAt = _clock.UtcNow; existing.UpdatedBy = actorStaffId; await _store.SaveChangesInTransactionAsync(); }
                return existing.Id;
            }

            var now = _clock.UtcNow;
            var created = new Customer { FullName = guestName, Phone = phone, Type = "GUEST", IsActive = true, IsDeleted = false, CreatedAt = now, UpdatedAt = now, CreatedBy = actorStaffId, UpdatedBy = actorStaffId };
            _store.AddCustomer(created);
            await _store.SaveChangesInTransactionAsync();
            return created.Id;
        }

        private static string? NormalizeRequestedServiceIds(string? raw)
        {
            var ids = string.IsNullOrWhiteSpace(raw) ? new List<long>() : raw.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(token => long.TryParse(token.Trim(), out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();
            return ids.Count > 0 ? string.Join(",", ids) : null;
        }

        private static string NormalizeReceptionPhone(string? raw) => string.Concat((raw ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch))).Trim();
        private static string? NormalizeReceptionText(string? raw) { var text = raw?.Trim(); return string.IsNullOrWhiteSpace(text) ? null : text; }
        private static string? NormalizeReceptionPlate(string? raw) { var value = NormalizeReceptionText(raw); return string.IsNullOrWhiteSpace(value) ? null : value.ToUpperInvariant(); }
    }
}
