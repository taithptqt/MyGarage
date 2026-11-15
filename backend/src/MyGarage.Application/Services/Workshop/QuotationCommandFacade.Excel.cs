using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class QuotationCommandFacade
    {
        public async Task<QuotationCommandResult<QuotationExcelExportResult>> ExportExcelAsync(
            long quotationId,
            CancellationToken cancellationToken)
        {
            var quotation = await _dataService.GetForExcelExportAsync(quotationId, cancellationToken);
            if (quotation == null)
            {
                return QuotationCommandResult<QuotationExcelExportResult>.NotFound("Không tìm thấy báo giá.");
            }

            var serviceOrder = quotation.ServiceOrder;
            if (serviceOrder == null)
            {
                return QuotationCommandResult<QuotationExcelExportResult>.BadRequest("Báo giá chưa liên kết lệnh sửa chữa hợp lệ.");
            }

            var detailIds = quotation.QuotationDetails
                .Where(x => !string.Equals(x.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();

            var supplementarySignals = await _dataService.GetSupplementarySignalsAsync(
                serviceOrder.Id,
                detailIds,
                cancellationToken);

            var exportResult = _quotationExcelService.ExportQuotation(quotation, supplementarySignals);
            return QuotationCommandResult<QuotationExcelExportResult>.Ok(exportResult);
        }

        public async Task<QuotationCommandResult<QuotationResponseDto>> ImportExcelAsync(
            long quotationId,
            string originalFileName,
            byte[] fileBytes,
            long? actorStaffId,
            long? currentUserId,
            string? note,
            CancellationToken cancellationToken)
        {
            if (quotationId <= 0)
            {
                return QuotationCommandResult<QuotationResponseDto>.BadRequest("Mã báo giá không hợp lệ.");
            }

            var extension = Path.GetExtension(originalFileName)?.Trim().ToLowerInvariant() ?? string.Empty;
            if (extension != ".xlsx")
            {
                return QuotationCommandResult<QuotationResponseDto>.BadRequest("Chỉ hỗ trợ file .xlsx được xuất từ hệ thống.");
            }

            var resolvedActorStaffId = ResolveActorStaffId(actorStaffId, currentUserId);
            if (!resolvedActorStaffId.HasValue || resolvedActorStaffId.Value <= 0)
            {
                return QuotationCommandResult<QuotationResponseDto>.BadRequest("Thiếu actorStaffId hợp lệ.");
            }

            var quotation = await _dataService.GetWithDetailsAsNoTrackingAsync(quotationId, cancellationToken);
            if (quotation == null)
            {
                return QuotationCommandResult<QuotationResponseDto>.NotFound("Không tìm thấy báo giá.");
            }

            await using var inputStream = new MemoryStream(fileBytes);
            var parseResult = _quotationExcelService.ParseSplitBillingImport(inputStream, quotation);
            if (!parseResult.IsValid)
            {
                return QuotationCommandResult<QuotationResponseDto>.BadRequest(parseResult.ErrorMessage ?? "Dữ liệu Excel không hợp lệ.");
            }

            try
            {
                var updated = await _quotationWorkflowService.UpdateSplitBillingAsync(
                    quotationId,
                    new QuotationSplitBillingUpdateRequestDto
                    {
                        ActorStaffId = resolvedActorStaffId.Value,
                        Note = string.IsNullOrWhiteSpace(note)
                            ? "Nhập phân bổ bảo hiểm từ file Excel."
                            : note.Trim(),
                        Items = parseResult.Items
                    });
                return QuotationCommandResult<QuotationResponseDto>.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return QuotationCommandResult<QuotationResponseDto>.BadRequest(ex.Message);
            }
        }
    }
}
