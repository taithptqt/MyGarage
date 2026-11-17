using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.AppointmentSchedulingHelpers;

namespace MyGarage.Services
{
    public class AppointmentBookingCoreService : IAppointmentBookingCoreService
    {
        private readonly IAppointmentStore _store;
        private readonly IGroupSessionService _groupSessionService;
        private readonly ISystemClock _clock;
        private readonly IAppointmentAvailabilityService _availabilityService;
        private readonly IAppointmentBaySelectionService _baySelectionService;
        private readonly IAppointmentReceptionService _receptionService;

        public AppointmentBookingCoreService(
            IAppointmentStore store,
            IGroupSessionService groupSessionService,
            ISystemClock clock,
            IAppointmentAvailabilityService availabilityService,
            IAppointmentBaySelectionService baySelectionService,
            IAppointmentReceptionService receptionService)
        {
            _store = store;
            _groupSessionService = groupSessionService;
            _clock = clock;
            _availabilityService = availabilityService;
            _baySelectionService = baySelectionService;
            _receptionService = receptionService;
        }

        public async Task EnsureBatchFixedLeadTimesAsync(
            long branchId,
            IReadOnlyCollection<BookAppointmentRequestDto> items)
        {
            var fixedLeadViolations = new List<string>();
            foreach (var item in items)
            {
                var flow = await _availabilityService.ResolveFlowAsync(item.BookingFlow, item.RequestedServiceIds, item.RequestedPackageIds);
                var (requestedStartAt, _) = ResolveRequestedWindow(flow, item);
                var opHour = await _store.GetBranchOperatingHourAsync(branchId, (int)requestedStartAt.DayOfWeek);
                var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);
                var minLeadMinutes = Math.Max(15, slotStepMinutes);
                if (requestedStartAt.Date == _clock.LocalNow.Date && requestedStartAt < _clock.LocalNow.AddMinutes(minLeadMinutes))
                {
                    fixedLeadViolations.Add($"{ResolveBookingItemLabel(item)} (cần cách hiện tại ít nhất {minLeadMinutes} phút)");
                }
            }

            if (fixedLeadViolations.Count > 0)
            {
                throw new InvalidOperationException("Các xe chưa đáp ứng điều kiện giờ cố định: " + string.Join("; ", fixedLeadViolations));
            }
        }

        public Task<Appointment> BookCoreAsync(long authUserId, BookAppointmentRequestDto dto, long? groupSessionId)
            => BookCoreInternalAsync(authUserId, dto, groupSessionId, false, Array.Empty<string>());

        public Task<Appointment> BookCoreDeferredHoldAsync(long authUserId, BookAppointmentRequestDto dto, long? groupSessionId, IReadOnlyCollection<string> processedHoldTokens)
            => BookCoreInternalAsync(authUserId, dto, groupSessionId, true, processedHoldTokens);

        public void ConsumeSlotHold(string? holdToken) => _availabilityService.ConsumeSlotHold(holdToken);

        private async Task<Appointment> BookCoreInternalAsync(
            long authUserId,
            BookAppointmentRequestDto dto,
            long? groupSessionId,
            bool deferHoldConsumption,
            IReadOnlyCollection<string> processedHoldTokens)
        {
            var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(dto.CreatedBy ?? authUserId);
            var rawServiceIds = (dto.RequestedServiceIds ?? string.Empty)
                .Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (rawServiceIds.Any(value => !long.TryParse(value, out var id) || id <= 0))
                throw new InvalidOperationException("Mã dịch vụ không hợp lệ.");

            var requestedServiceIdList = ParseIds(dto.RequestedServiceIds).Distinct().ToList();
            if (requestedServiceIdList.Count > 0 &&
                (await _store.GetActiveStandardServiceMapAsync(requestedServiceIdList)).Count != requestedServiceIdList.Count)
                throw new InvalidOperationException("Dịch vụ không tồn tại hoặc đã ngừng áp dụng.");
            var requestedPackageIdList = ParseIds(dto.RequestedPackageIds).Distinct().ToList();
            var flow = await _availabilityService.ResolveFlowAsync(dto.BookingFlow, dto.RequestedServiceIds, dto.RequestedPackageIds);
            var effectiveRequestedServiceIds = requestedServiceIdList.Count > 0
                ? string.Join(",", requestedServiceIdList)
                : await _availabilityService.ResolveRequestedServiceIdsForBookingAsync(dto.RequestedServiceIds, dto.RequestedPackageIds);
            var effectiveRequestedPackageIds = requestedPackageIdList.Count > 0
                ? await ResolveRequestedPackageIdsForBookingAsync(dto.RequestedPackageIds)
                : null;
            long actualCustomerId = dto.CustomerId ?? authUserId;
            var (requestedStartAt, requestedEndAt) = ResolveRequestedWindow(flow, dto);
            ValidateBookingBasics(dto, flow, requestedServiceIdList, requestedPackageIdList, requestedStartAt);
            await _receptionService.EnsureInsuranceCompanyExistsAsync(dto.InsuranceCompanyId);

            var opHour = await _store.GetBranchOperatingHourAsync(dto.BranchId, (int)requestedStartAt.DayOfWeek);
            if (opHour?.IsDayOff == true)
            {
                throw new InvalidOperationException("Chi nhánh không hoạt động vào ngày này.");
            }

            var openTime = opHour?.OpenTime ?? new TimeOnly(8, 0);
            var closeTime = opHour?.CloseTime ?? new TimeOnly(20, 0);
            var slotStepMinutes = ResolveSlotStepMinutes(opHour?.SlotDurationMinutes);

            var vehicleModelId = await _receptionService.ResolveVehicleModelIdForBookingAsync(actualCustomerId, dto);
            await _baySelectionService.EnsureRequestedServicesCompatibleWithVehicleEngineAsync(
                vehicleModelId,
                effectiveRequestedServiceIds,
                effectiveRequestedPackageIds);
            var partAvailability = await _baySelectionService.EvaluatePartAvailabilityForBookingAsync(
                dto.BranchId,
                vehicleModelId,
                effectiveRequestedServiceIds,
                effectiveRequestedPackageIds);

            var plannedDurationMinutes = await _availabilityService.ResolveScheduledDurationAsync(
                effectiveRequestedServiceIds,
                slotStepMinutes,
                false,
                effectiveRequestedPackageIds);

            if (flow == BookingFlowType.Fixed &&
                ShouldFallbackToFlexibleFlowForLongDuration(plannedDurationMinutes, openTime, closeTime))
            {
                flow = BookingFlowType.Flexible;
            }

            if (flow == BookingFlowType.Fixed)
            {
                var minLeadMinutes = Math.Max(15, slotStepMinutes);
                if (requestedStartAt.Date == _clock.LocalNow.Date && requestedStartAt < _clock.LocalNow.AddMinutes(minLeadMinutes))
                {
                    throw new InvalidOperationException("Thời gian hẹn phải cách hiện tại ít nhất " + minLeadMinutes + " phút.");
                }

                requestedEndAt = requestedStartAt.AddMinutes(plannedDurationMinutes);
            }
            else if (requestedEndAt <= requestedStartAt)
            {
                requestedEndAt = requestedStartAt.AddMinutes(_availabilityService.ResolveFlexibleWindowMinutes(slotStepMinutes));
            }

            if (requestedEndAt <= requestedStartAt)
            {
                throw new InvalidOperationException("Giờ kết thúc phải sau giờ bắt đầu.");
            }

            ValidateTimeWindow(requestedStartAt, requestedEndAt, openTime, closeTime);
            _availabilityService.ValidateSlotHoldOrThrow(dto, flow, requestedStartAt, requestedEndAt);

            var health = await _availabilityService.GetBranchBookingHealthAsync(dto.BranchId, _clock.LocalNow);
            if (health.IsBookingLocked && health.LockUntil.HasValue && requestedStartAt < health.LockUntil.Value)
            {
                throw new InvalidOperationException($"Chi nhánh đang tạm khóa nhận lịch đến {health.LockUntil:HH:mm dd/MM/yyyy} do quá tải.");
            }

            var requestedCluster = await _availabilityService.ResolveServiceClusterAsync(effectiveRequestedServiceIds, effectiveRequestedPackageIds, dto.CustomerNotes);
            if (flow == BookingFlowType.Flexible)
            {
                await _baySelectionService.EnsureFlexibleQueueCapacityAsync(dto.BranchId, requestedStartAt, requestedEndAt, dto.SlotHoldToken, processedHoldTokens);
            }
            else
            {
                await _baySelectionService.EnsureFixedFlowCapacityAsync(dto.BranchId, requestedStartAt, requestedEndAt, requestedCluster, dto.SlotHoldToken, processedHoldTokens);
            }

            var created = await PersistBookingAsync(
                actorStaffId,
                dto,
                actualCustomerId,
                groupSessionId,
                requestedStartAt,
                requestedEndAt,
                flow,
                effectiveRequestedServiceIds,
                effectiveRequestedPackageIds,
                requestedCluster,
                vehicleModelId,
                plannedDurationMinutes,
                slotStepMinutes,
                partAvailability);

            if (!deferHoldConsumption)
                _availabilityService.ConsumeSlotHold(dto.SlotHoldToken);

            return created;
        }

        private async Task<Appointment> PersistBookingAsync(
            long? actorStaffId,
            BookAppointmentRequestDto dto,
            long actualCustomerId,
            long? groupSessionId,
            DateTime requestedStartAt,
            DateTime requestedEndAt,
            BookingFlowType flow,
            string? effectiveRequestedServiceIds,
            string? effectiveRequestedPackageIds,
            ServiceCluster requestedCluster,
            long? validatedVehicleModelId,
            int plannedDurationMinutes,
            int slotStepMinutes,
            PartAvailabilityEvaluation partAvailability)
        {
            return await _store.ExecuteInTransactionAsync(async () =>
                await PersistBookingInternalAsync(
                    actorStaffId, dto, actualCustomerId, groupSessionId,
                    requestedStartAt, requestedEndAt, flow,
                    effectiveRequestedServiceIds, effectiveRequestedPackageIds,
                    requestedCluster, validatedVehicleModelId,
                    plannedDurationMinutes, slotStepMinutes, partAvailability));
        }

        private async Task<Appointment> PersistBookingInternalAsync(
            long? actorStaffId,
            BookAppointmentRequestDto dto,
            long actualCustomerId,
            long? groupSessionId,
            DateTime requestedStartAt,
            DateTime requestedEndAt,
            BookingFlowType flow,
            string? effectiveRequestedServiceIds,
            string? effectiveRequestedPackageIds,
            ServiceCluster requestedCluster,
            long? validatedVehicleModelId,
            int plannedDurationMinutes,
            int slotStepMinutes,
            PartAvailabilityEvaluation partAvailability)
        {
            var customer = await _store.GetCustomerByIdAsync(actualCustomerId);
            if (customer == null) throw new InvalidOperationException("Không tìm thấy khách hàng để đặt lịch.");

            await _receptionService.ValidateSelectedVehicleOwnershipAsync(actualCustomerId, dto.VehicleId);

            _receptionService.UpdateCustomerContact(customer, dto);
            var vehicleId = await _receptionService.ResolveVehicleForBookingAsync(actualCustomerId, actorStaffId, dto);

            var appointment = new Appointment
            {
                BranchId = dto.BranchId,
                CustomerId = actualCustomerId,
                VehicleId = vehicleId,
                GroupSessionId = groupSessionId,
                Status = nameof(AppointmentStatus.PENDING),
                CreatedBy = actorStaffId,
                CustomerNotes = dto.CustomerNotes,
                StartTime = requestedStartAt,
                EndTime = requestedEndAt,
                RequestedServiceIds = effectiveRequestedServiceIds,
                RequestedPackageIds = effectiveRequestedPackageIds,
                InsuranceCompanyId = dto.InsuranceCompanyId.HasValue && dto.InsuranceCompanyId.Value > 0
                    ? dto.InsuranceCompanyId
                    : null,
                InsuranceDeductibleAmount = _receptionService.NormalizeInsuranceDeductibleAmount(dto.InsuranceDeductibleAmount),
                InsuranceDeductibleOption = _receptionService.NormalizeInsuranceToken(dto.InsuranceDeductibleOption),
                InsuranceClaimMode = _receptionService.NormalizeInsuranceToken(dto.InsuranceClaimMode),
                InsuranceQuoteState = _receptionService.NormalizeInsuranceToken(dto.InsuranceQuoteState),
                CreatedAt = _clock.LocalNow,
                UpdatedAt = _clock.LocalNow
            };

            _store.AddAppointment(appointment);
            await _store.SaveChangesInTransactionAsync();

            await _receptionService.PersistAppointmentInsuranceAssetsAsync(appointment, dto);

            var effectiveVehicleModelId = validatedVehicleModelId;
            if (!effectiveVehicleModelId.HasValue && vehicleId.HasValue)
            {
                effectiveVehicleModelId = await _store.GetVehicleModelIdAsync(vehicleId.Value);
            }

            await _baySelectionService.ApplyBookingReservationArtifactsAsync(
                appointment,
                flow,
                requestedCluster,
                effectiveVehicleModelId,
                effectiveRequestedServiceIds,
                effectiveRequestedPackageIds,
                actorStaffId,
                plannedDurationMinutes,
                slotStepMinutes,
                partAvailability);

            if (groupSessionId.HasValue)
                await _groupSessionService.SyncCountersAsync(groupSessionId.Value, actorStaffId);

            return appointment;
        }

        private async Task<string?> ResolveRequestedPackageIdsForBookingAsync(string? requestedPackageIds)
        {
            var normalized = NormalizeRequestedIdsForStorage(requestedPackageIds);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var packageIds = ParseIds(normalized).Distinct().ToList();
            var existingCount = await _store.CountActivePackagesByIdsAsync(packageIds);

            if (existingCount != packageIds.Count)
            {
                throw new InvalidOperationException("Gói dịch vụ không hợp lệ hoặc đã ngừng áp dụng.");
            }

            return normalized;
        }

        private static (DateTime startAt, DateTime endAt) ResolveRequestedWindow(
            BookingFlowType flow,
            BookAppointmentRequestDto dto)
        {
            if (flow == BookingFlowType.Flexible)
            {
                var startAt = dto.CheckInWindowStart ?? dto.StartTime
                    ?? throw new InvalidOperationException("Cần cung cấp khung giờ check-in bắt đầu.");
                var endAt = dto.CheckInWindowEnd ?? dto.EndTime ?? startAt;
                startAt = NormalizeRequestDateTimeToLocalClock(startAt);
                endAt = NormalizeRequestDateTimeToLocalClock(endAt);
                return (startAt, endAt);
            }

            var fixedStartAt = dto.StartTime
                ?? throw new InvalidOperationException("Cần cung cấp giờ bắt đầu cho lịch có giờ.");
            var fixedEndAt = dto.EndTime ?? fixedStartAt;
            fixedStartAt = NormalizeRequestDateTimeToLocalClock(fixedStartAt);
            fixedEndAt = NormalizeRequestDateTimeToLocalClock(fixedEndAt);
            return (fixedStartAt, fixedEndAt);
        }

        private static DateTime NormalizeRequestDateTimeToLocalClock(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return VietnamTime.ToVietnamTime(value);
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
                throw new InvalidOperationException("Không thể đặt lịch cho ngày trong quá khứ.");
            if (string.IsNullOrWhiteSpace(dto.CustomerFullName))
                throw new InvalidOperationException("Vui lòng nhập họ tên khách hàng.");
            if (string.IsNullOrWhiteSpace(dto.CustomerPhone))
                throw new InvalidOperationException("Vui lòng nhập số điện thoại khách hàng.");
            if (flow == BookingFlowType.Fixed && requestedServiceIds.Count == 0 && requestedPackageIds.Count == 0)
                throw new InvalidOperationException("Luồng giờ cố định yêu cầu chọn ít nhất một dịch vụ cụ thể.");

            if (!dto.VehicleId.HasValue)
            {
                if (!dto.VehicleModelId.HasValue)
                    throw new InvalidOperationException("Vui lòng chọn dòng xe.");
                if (string.IsNullOrWhiteSpace(dto.LicensePlate))
                    throw new InvalidOperationException("Vui lòng nhập biển số xe.");
                if (!dto.ManufactureYear.HasValue || dto.ManufactureYear.Value <= 0)
                    throw new InvalidOperationException("Vui lòng chọn năm sản xuất.");
            }
        }

        private static void ValidateTimeWindow(DateTime startAt, DateTime endAt, TimeOnly openTime, TimeOnly closeTime)
        {
            if (endAt <= startAt) throw new InvalidOperationException("Giờ kết thúc phải sau giờ bắt đầu.");
            if (startAt.Date != endAt.Date) throw new InvalidOperationException("Lịch hẹn phải nằm trong cùng một ngày.");
            var start = TimeOnly.FromDateTime(startAt);
            var end = TimeOnly.FromDateTime(endAt);
            if (start < openTime || end > closeTime)
                throw new InvalidOperationException($"Lịch hẹn phải trong giờ làm việc {openTime:HH\\:mm}-{closeTime:HH\\:mm}.");
        }

        private static string ResolveBookingItemLabel(BookAppointmentRequestDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.LicensePlate))
                return $"Xe {dto.LicensePlate.Trim()}";
            if (dto.VehicleId.HasValue)
                return $"Xe ID {dto.VehicleId.Value}";
            if (!string.IsNullOrWhiteSpace(dto.Vin))
                return $"Xe VIN {dto.Vin.Trim()}";
            return "Xe chưa rõ biển số";
        }
    }
}
