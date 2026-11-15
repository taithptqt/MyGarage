using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentsCommandFacade : IAppointmentsCommandFacade
    {
        private readonly IAppointmentReadService _appointmentReadService;
        private readonly IAppointmentBookingCommandService _appointmentBookingService;
        private readonly IAppointmentLifecycleCommandService _appointmentLifecycleService;
        private readonly ICloudStorageService _cloudStorageService;
        private readonly IAppointmentDataService _dataService;
        private readonly IAppointmentEditRequestService _editRequestService;

        public AppointmentsCommandFacade(
            IAppointmentReadService appointmentReadService,
            IAppointmentBookingCommandService appointmentBookingService,
            IAppointmentLifecycleCommandService appointmentLifecycleService,
            ICloudStorageService cloudStorageService,
            IAppointmentDataService dataService,
            IAppointmentEditRequestService editRequestService)
        {
            _appointmentReadService = appointmentReadService;
            _appointmentBookingService = appointmentBookingService;
            _appointmentLifecycleService = appointmentLifecycleService;
            _cloudStorageService = cloudStorageService;
            _dataService = dataService;
            _editRequestService = editRequestService;
        }

        internal AppointmentsCommandFacade(
            IAppointmentReadService appointmentReadService,
            IAppointmentBookingCommandService appointmentBookingService,
            IAppointmentLifecycleCommandService appointmentLifecycleService,
            IAuditLogService auditLogService,
            ICloudStorageService cloudStorageService,
            IAppointmentDataService dataService)
            : this(
                appointmentReadService,
                appointmentBookingService,
                appointmentLifecycleService,
                cloudStorageService,
                dataService,
                new AppointmentEditRequestService(appointmentReadService, appointmentLifecycleService, auditLogService, dataService))
        {
        }

        public async Task<PagedEnvelope<object>> GetPagedAsync(
            long? branchId,
            long? customerId,
            string? status,
            int? page,
            int? pageSize,
            AppointmentActorContext actor,
            bool forceCustomerShape = false)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (items, total) = await _appointmentReadService.GetDetailedPageAsync(branchId, customerId, status, normalizedPage, normalizedSize);
            var mapped = MapAppointmentsResponse(items, actor, forceCustomerShape);
            return PagedEnvelope<object>.Create(mapped, total, normalizedPage, normalizedSize);
        }

        public async Task<AppointmentsCommandResult<object?>> GetByIdAsync(
            long id,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default)
        {
            var scopeMeta = await _dataService.GetScopeMetaAsync(id, cancellationToken);
            if (!scopeMeta.Found)
                return AppointmentsCommandResult<object?>.NotFound();

            if (!AppointmentEditRequestService.CanAccessAppointment(scopeMeta.CustomerId, scopeMeta.BranchId, actor))
                return AppointmentsCommandResult<object?>.Forbid();

            var dto = await _appointmentReadService.GetDetailedAsync(id);
            return dto == null
                ? AppointmentsCommandResult<object?>.NotFound()
                : AppointmentsCommandResult<object?>.Ok(MapAppointmentResponse(dto, actor));
        }

        public async Task<AppointmentsCommandResult<AppointmentCreatedPayload>> CreateAsync(
            long actorUserId,
            BookAppointmentRequestDto dto,
            AppointmentActorContext actor)
        {
            dto.CreatedBy = actorUserId;

            try
            {
                var appointment = await _appointmentBookingService.BookAsync(actorUserId, dto);
                var detail = await _appointmentReadService.GetDetailedAsync(appointment.Id);
                return AppointmentsCommandResult<AppointmentCreatedPayload>.Created(
                    new AppointmentCreatedPayload(appointment.Id, MapAppointmentResponse(detail, actor)));
            }
            catch (InvalidOperationException ex)
            {
                return AppointmentsCommandResult<AppointmentCreatedPayload>.BadRequest(ex.Message);
            }
        }

        public async Task<AppointmentsCommandResult<AppointmentCreatedPayload>> CreateGuestAsync(
            BookAppointmentRequestDto dto)
        {
            if (!dto.CustomerId.HasValue)
            {
                return AppointmentsCommandResult<AppointmentCreatedPayload>.BadRequest("Vui lòng cung cấp CustomerId cho luồng guest.");
            }

            try
            {
                dto.CreatedBy = null;
                var appointment = await _appointmentBookingService.BookAsync(dto.CustomerId.Value, dto);
                var detail = await _appointmentReadService.GetDetailedAsync(appointment.Id);
                return AppointmentsCommandResult<AppointmentCreatedPayload>.Created(
                    new AppointmentCreatedPayload(appointment.Id, MapAppointmentResponse(detail, actor: null, forceCustomerShape: true)));
            }
            catch (InvalidOperationException ex)
            {
                return AppointmentsCommandResult<AppointmentCreatedPayload>.BadRequest(ex.Message);
            }
        }

        public async Task<AppointmentsCommandResult<ReceptionAppointmentSaveResponseDto>> SaveReceptionBookingAsync(
            ReceptionAppointmentSaveRequestDto? dto,
            AppointmentActorContext actor)
        {
            if (dto == null)
                return AppointmentsCommandResult<ReceptionAppointmentSaveResponseDto>.BadRequest("Dữ liệu đặt lịch không hợp lệ.");
            if (!actor.IsStaffUser)
                return AppointmentsCommandResult<ReceptionAppointmentSaveResponseDto>.Forbid();
            if (!actor.IsAdminStaff && dto.BranchId > 0 && dto.BranchId != actor.CurrentBranchId)
                return AppointmentsCommandResult<ReceptionAppointmentSaveResponseDto>.Forbid();
            if (actor.CurrentUserId == null)
                return AppointmentsCommandResult<ReceptionAppointmentSaveResponseDto>.Unauthorized();

            try
            {
                var result = await _appointmentBookingService.SaveReceptionBookingAsync(actor.CurrentUserId.Value, dto);
                return AppointmentsCommandResult<ReceptionAppointmentSaveResponseDto>.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return AppointmentsCommandResult<ReceptionAppointmentSaveResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<AppointmentsCommandResult<object>> UploadInsuranceAssetAsync(
            string fileName,
            string contentType,
            byte[] fileBytes,
            string? assetType,
            CancellationToken cancellationToken = default)
        {
            var normalizedAssetType = NormalizeInsuranceAssetType(assetType);
            if (normalizedAssetType == null)
            {
                return AppointmentsCommandResult<object>.BadRequest("Loại ảnh bảo hiểm không hợp lệ.");
            }

            try
            {
                var uploaded = await SaveInsuranceAssetFileAsync(fileName, contentType, fileBytes, normalizedAssetType, cancellationToken);
                await _dataService.AddInsuranceUploadAsync(new AppointmentInsuranceUpload
                {
                    AssetType = normalizedAssetType,
                    BlobPath = uploaded.BlobPath,
                    FileUrl = uploaded.Url,
                    Status = AppointmentInsuranceUploadStatuses.Staged
                }, cancellationToken);

                return AppointmentsCommandResult<object>.Ok(new { url = uploaded.Url, assetType = normalizedAssetType });
            }
            catch
            {
                return AppointmentsCommandResult<object>.BadRequest("Không thể tải ảnh bảo hiểm lên máy chủ.");
            }
        }

        public async Task<AppointmentsCommandResult<object?>> UpdateStatusAsync(
            long id,
            string? status,
            string? reason,
            DateTime? arrivedAt,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default)
        {
            var requested = (status ?? string.Empty).Trim().ToUpperInvariant();
            if (requested == "CANCELLED")
            {
                requested = "CANCELED";
            }

            if (string.IsNullOrWhiteSpace(requested))
            {
                return AppointmentsCommandResult<object?>.BadRequest("Status is required.");
            }

            if (!Enum.TryParse<AppointmentPatchAction>(requested, ignoreCase: true, out var action))
            {
                return AppointmentsCommandResult<object?>.BadRequest("Unsupported status transition.");
            }

            var scopeMeta = await _dataService.GetScopeMetaAsync(id, cancellationToken);
            if (!scopeMeta.Found)
                return AppointmentsCommandResult<object?>.NotFound();
            if (!AppointmentEditRequestService.CanAccessAppointment(scopeMeta.CustomerId, scopeMeta.BranchId, actor))
                return AppointmentsCommandResult<object?>.Forbid();
            if (!actor.IsStaffUser && action != AppointmentPatchAction.CANCELED)
                return AppointmentsCommandResult<object?>.Forbid();

            try
            {
                Appointment? appointment = action switch
                {
                    AppointmentPatchAction.CONFIRMED => await _appointmentLifecycleService.ConfirmAsync(id, actor.CurrentUserId),
                    AppointmentPatchAction.CANCELED => await _appointmentLifecycleService.CancelAsync(id, reason, actor.CurrentUserId),
                    AppointmentPatchAction.CHECKED_IN => await _appointmentLifecycleService.CheckInAsync(id, arrivedAt, actor.CurrentUserId),
                    AppointmentPatchAction.ARRIVED => await _appointmentLifecycleService.ArriveAsync(id, arrivedAt, actor.CurrentUserId),
                    _ => null
                };

                if (appointment == null)
                {
                    return AppointmentsCommandResult<object?>.NotFound();
                }

                var detail = await _appointmentReadService.GetDetailedAsync(id);
                return AppointmentsCommandResult<object?>.Ok(MapAppointmentResponse(detail, actor));
            }
            catch (InvalidOperationException ex)
            {
                return AppointmentsCommandResult<object?>.BadRequest(ex.Message);
            }
        }

        public Task<AppointmentsCommandResult<AppointmentEditCapabilityResponseDto>> GetEditCapabilitiesAsync(
            long id, AppointmentActorContext actor, CancellationToken cancellationToken = default)
            => _editRequestService.GetEditCapabilitiesAsync(id, actor, cancellationToken);

        public Task<AppointmentsCommandResult<IReadOnlyList<AppointmentEditRequestItemDto>>> GetEditRequestsAsync(
            long id, string? status, AppointmentActorContext actor, CancellationToken cancellationToken = default)
            => _editRequestService.GetEditRequestsAsync(id, status, actor, cancellationToken);

        public Task<AppointmentsCommandResult> RequestEditAsync(
            long id, EditRequestCreateDto? dto, AppointmentActorContext actor)
            => _editRequestService.RequestEditAsync(id, dto, actor);

        public Task<AppointmentsCommandResult> DecideEditRequestAsync(
            long id, long requestId, EditRequestDecisionDto? dto, AppointmentActorContext actor, CancellationToken cancellationToken = default)
            => _editRequestService.DecideEditRequestAsync(id, requestId, dto, actor, cancellationToken);

        private async Task<(string Url, string BlobPath)> SaveInsuranceAssetFileAsync(
            string originalFileName,
            string contentType,
            byte[] fileBytes,
            string assetType,
            CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(originalFileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var folderName = string.Equals(assetType, AppointmentInsuranceAssetTypes.Card, StringComparison.OrdinalIgnoreCase)
                ? "appointments/insurance-cards"
                : "appointments/insurance-damage";
            var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            var blobPath = $"{folderName.Trim('/')}/{fileName}";
            await using var stream = new MemoryStream(fileBytes);
            var url = await _cloudStorageService.UploadAsync(stream, folderName, fileName, contentType, cancellationToken);
            return (url, blobPath);
        }

        private static string? NormalizeInsuranceAssetType(string? assetType)
        {
            var normalized = string.IsNullOrWhiteSpace(assetType)
                ? string.Empty
                : assetType.Trim().ToUpperInvariant();

            return normalized switch
            {
                AppointmentInsuranceAssetTypes.Card => AppointmentInsuranceAssetTypes.Card,
                AppointmentInsuranceAssetTypes.Damage => AppointmentInsuranceAssetTypes.Damage,
                _ => null
            };
        }

        private static IEnumerable<object> MapAppointmentsResponse(
            IEnumerable<AppointmentDetailResponseDto> appointments,
            AppointmentActorContext? actor,
            bool forceCustomerShape = false)
        {
            var list = appointments?.ToList() ?? new List<AppointmentDetailResponseDto>();
            return ShouldHidePartsFollowUp(actor, forceCustomerShape)
                ? list.Select(item => item.ToCustomerResponse()).ToList()
                : list;
        }

        private static object? MapAppointmentResponse(
            AppointmentDetailResponseDto? appointment,
            AppointmentActorContext? actor,
            bool forceCustomerShape = false)
            => appointment == null
                ? null
                : ShouldHidePartsFollowUp(actor, forceCustomerShape)
                    ? appointment.ToCustomerResponse()
                    : appointment;

        private static bool ShouldHidePartsFollowUp(AppointmentActorContext? actor, bool forceCustomerShape = false)
            => forceCustomerShape ||
               (actor != null &&
                (IsCustomerRole(actor.ClaimRole) ||
                 IsCustomerRole(actor.ClaimRoleCode) ||
                 IsCustomerRole(actor.ClaimRoleName)));

        private static bool IsCustomerRole(string? roleValue)
        {
            if (string.IsNullOrWhiteSpace(roleValue))
            {
                return false;
            }

            var normalized = AppointmentEditRequestService.NormalizeSearchToken(roleValue);
            return normalized.Contains("CUSTOMER", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("KHACH_HANG", StringComparison.OrdinalIgnoreCase);
        }
    }
}
