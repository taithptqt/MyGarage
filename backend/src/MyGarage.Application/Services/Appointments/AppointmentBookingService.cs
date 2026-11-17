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
    public class AppointmentBookingService : IAppointmentBookingService
    {
        private readonly IAppointmentStore _store;
        private readonly IGroupSessionService _groupSessionService;
        private readonly IAppointmentDetailAssembler _appointmentDetailAssembler;
        private readonly IAppointmentNotificationService _notificationService;
        private readonly IAppointmentReceptionService _receptionService;
        private readonly IAppointmentBookingCoreService _bookingCoreService;

        public AppointmentBookingService(
            IAppointmentStore store,
            IGroupSessionService groupSessionService,
            IAppointmentDetailAssembler appointmentDetailAssembler,
            IAppointmentNotificationService notificationService,
            IAppointmentReceptionService receptionService,
            IAppointmentBookingCoreService bookingCoreService)
        {
            _store = store;
            _groupSessionService = groupSessionService;
            _appointmentDetailAssembler = appointmentDetailAssembler;
            _notificationService = notificationService;
            _receptionService = receptionService;
            _bookingCoreService = bookingCoreService;
        }

        public async Task<Appointment> BookAsync(long authUserId, BookAppointmentRequestDto dto)
        {
            var appointment = await _bookingCoreService.BookCoreAsync(authUserId, dto, null);
            await _notificationService.TrySendBookingSuccessEmailAsync(appointment.Id);
            return appointment;
        }

        public async Task<ReceptionAppointmentSaveResponseDto> SaveReceptionBookingAsync(long actorUserId, ReceptionAppointmentSaveRequestDto dto)
        {
            await _receptionService.ValidateReceptionSaveRequestAsync(dto);

            var plans = dto.VehiclePlans!.Where(plan => plan != null).ToList();
            var replaceAppointmentId = dto.ReplaceAppointmentId.GetValueOrDefault();
            EnsureDistinctHoldTokens(plans.Select(plan => plan.SlotHoldToken));

            var committed = await _store.ExecuteInTransactionAsync(async () =>
            {
                var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId);
                var resolvedCustomerId = await _receptionService.ResolveReceptionCustomerIdAsync(dto, actorStaffId);

                long? groupSessionId = null;
                if (plans.Count > 1)
                {
                    var customerNote = string.IsNullOrWhiteSpace(dto.CustomerNotes?.Trim())
                        ? null
                        : dto.CustomerNotes.Trim();
                    groupSessionId = (await _groupSessionService.CreateAsync(
                        resolvedCustomerId,
                        dto.BranchId,
                        customerNote,
                        actorStaffId)).Id;
                }

                var createdAppointments = new List<Appointment>(plans.Count);
                var processedHoldTokens = new List<string>();
                foreach (var plan in plans)
                {
                    var request = await _receptionService.BuildReceptionBookRequestAsync(dto, plan, resolvedCustomerId, actorStaffId);
                    var created = await _bookingCoreService.BookCoreDeferredHoldAsync(actorUserId, request, groupSessionId, processedHoldTokens);
                    createdAppointments.Add(created);
                    if (!string.IsNullOrWhiteSpace(request.SlotHoldToken)) processedHoldTokens.Add(request.SlotHoldToken.Trim());
                }

                await _receptionService.CompleteReceptionReplacementAsync(actorUserId, replaceAppointmentId, createdAppointments);

                if (groupSessionId.HasValue)
                    await _groupSessionService.SyncCountersAsync(groupSessionId.Value, actorStaffId);

                var response = await _receptionService.BuildReceptionSaveResponseAsync(
                    resolvedCustomerId,
                    groupSessionId,
                    replaceAppointmentId > 0,
                    createdAppointments);
                return (Response: response, AppointmentIds: createdAppointments.Select(a => a.Id).ToList(), HoldTokens: processedHoldTokens);
            });
            foreach (var holdToken in committed.HoldTokens) _bookingCoreService.ConsumeSlotHold(holdToken);
            await _notificationService.TrySendBookingSuccessEmailAsync(committed.AppointmentIds);
            return committed.Response;
        }

        private static void EnsureDistinctHoldTokens(IEnumerable<string?> holdTokens)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in holdTokens)
            {
                if (!string.IsNullOrWhiteSpace(token) && !seen.Add(token.Trim()))
                    throw new InvalidOperationException("Mỗi lịch trong nhóm phải có giữ chỗ riêng.");
            }
        }

        public async Task<GroupSessionResponseDto> BookBatchAsync(long authUserId, BookAppointmentBatchRequestDto dto)
        {
            return await BookBatchForCustomerAsync(authUserId, authUserId, dto);
        }

        public async Task<GroupSessionResponseDto> BookBatchForCustomerAsync(long actorUserId, long targetCustomerId, BookAppointmentBatchRequestDto dto)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                throw new InvalidOperationException("Danh sách lịch hẹn không được để trống.");

            if (targetCustomerId <= 0)
                throw new InvalidOperationException("Khách hàng đặt lịch không hợp lệ.");

            if (dto.Items.Any(item => item == null || (item.CustomerId.HasValue && item.CustomerId.Value != targetCustomerId)))
                throw new InvalidOperationException("Mỗi lịch trong nhóm phải thuộc cùng khách hàng.");

            EnsureDistinctHoldTokens(dto.Items.Select(item => item.SlotHoldToken));
            await _bookingCoreService.EnsureBatchFixedLeadTimesAsync(dto.BranchId, dto.Items);

            var committed = await _store.ExecuteInTransactionAsync(async () =>
            {
                var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId);
                var group = await _groupSessionService.CreateAsync(targetCustomerId, dto.BranchId, dto.CustomerNote, actorStaffId);

                var createdAppointmentIds = new List<long>();
                var processedHoldTokens = new List<string>();
                foreach (var item in dto.Items)
                {
                    item.BranchId = dto.BranchId;
                    item.CreatedBy = actorStaffId;
                    item.CustomerId ??= targetCustomerId;
                    var created = await _bookingCoreService.BookCoreDeferredHoldAsync(actorUserId, item, group.Id, processedHoldTokens);
                    createdAppointmentIds.Add(created.Id);
                    if (!string.IsNullOrWhiteSpace(item.SlotHoldToken)) processedHoldTokens.Add(item.SlotHoldToken.Trim());
                }

                await _groupSessionService.SyncCountersAsync(group.Id, actorStaffId);

                var response = await _groupSessionService.GetByCustomerAndIdAsync(targetCustomerId, group.Id);
                if (response != null)
                {
                    var createdAppointments = await _store.GetAppointmentsForEmailAsync(createdAppointmentIds);

                    response.Appointments = (await _appointmentDetailAssembler.AssembleManyAsync(createdAppointments))
                        .ToList();
                    return (Response: response, AppointmentIds: createdAppointmentIds, HoldTokens: processedHoldTokens);
                }

                throw new InvalidOperationException("Không thể tải thông tin nhóm lịch hẹn sau khi tạo.");
            });
            foreach (var holdToken in committed.HoldTokens) _bookingCoreService.ConsumeSlotHold(holdToken);
            await _notificationService.TrySendBookingSuccessEmailAsync(committed.AppointmentIds);
            return committed.Response;
        }
    }
}
