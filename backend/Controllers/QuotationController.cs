using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[Route("api")]
[ApiController]
public class QuotationController : ControllerBase
{
    private readonly IQuotationWorkflowService _quotationWorkflowService;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IGarageContext _context;
    private const long MaxInsuranceEvidenceBytes = 10 * 1024 * 1024;

    public QuotationController(
        IQuotationWorkflowService quotationWorkflowService,
        ICloudStorageService cloudStorageService,
        IGarageContext context)
    {
        _quotationWorkflowService = quotationWorkflowService;
        _cloudStorageService = cloudStorageService;
        _context = context;
    }

    [HttpPost("service-orders/{serviceOrderId:long}/quotations")]
    public async Task<ActionResult<QuotationResponseDto>> CreateQuotation(
        long serviceOrderId,
        [FromBody] QuotationCreateRequestDto request)
    {
        try
        {
            var quotation = await _quotationWorkflowService.CreateQuotationAsync(serviceOrderId, request);
            return Ok(quotation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("service-orders/{serviceOrderId:long}/quotations")]
    public async Task<ActionResult> GetByServiceOrder(long serviceOrderId)
    {
        var rows = await _quotationWorkflowService.GetQuotationsByServiceOrderAsync(serviceOrderId);
        return Ok(rows);
    }

    [HttpGet("service-orders/{serviceOrderId:long}/mandatory-overrides")]
    public async Task<ActionResult> GetMandatoryOverridesByServiceOrder(long serviceOrderId)
    {
        var rows = await _quotationWorkflowService.GetMandatoryOverrideRequestsByServiceOrderAsync(serviceOrderId);
        return Ok(rows);
    }

    [HttpGet("quotations/{quotationId:long}")]
    public async Task<ActionResult<QuotationResponseDto>> GetById(long quotationId)
    {
        var row = await _quotationWorkflowService.GetQuotationAsync(quotationId);
        if (row == null)
        {
            return NotFound(new { message = "Không tìm thấy báo giá." });
        }

        return Ok(row);
    }

    [HttpPost("quotations/{quotationId:long}/approve")]
    public async Task<ActionResult<QuotationResponseDto>> Approve(long quotationId, [FromBody] QuotationApproveRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.ApproveQuotationAsync(quotationId, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/{quotationId:long}/customer-decision")]
    public async Task<ActionResult<QuotationResponseDto>> ApplyCustomerDecision(
        long quotationId,
        [FromBody] QuotationCustomerDecisionRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.ApplyCustomerDecisionAsync(quotationId, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/{quotationId:long}/clone")]
    public async Task<ActionResult<QuotationResponseDto>> CloneQuotation(
        long quotationId,
        [FromBody] QuotationCloneRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.CloneQuotationAsync(quotationId, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/{quotationId:long}/split-billing")]
    public async Task<ActionResult<QuotationResponseDto>> UpdateSplitBilling(
        long quotationId,
        [FromBody] QuotationSplitBillingUpdateRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.UpdateSplitBillingAsync(quotationId, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("mandatory-overrides/{requestId:long}/decision")]
    public async Task<ActionResult> DecideMandatoryOverride(
        long requestId,
        [FromBody] MandatoryOverrideDecisionRequestDto request)
    {
        try
        {
            await _quotationWorkflowService.DecideMandatoryOverrideAsync(requestId, request);
            return Ok(new { message = "Mandatory override updated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/submit")]
    public async Task<ActionResult<QuotationResponseDto>> Submit(long quotationId, [FromBody] QuotationStatusUpdateRequestDto request)
    {
        try
        {
            if (!this.IsServiceAdvisorOrReceptionist())
            {
                return Forbid();
            }

            var actorStaffId = ResolveActorStaffId(request?.ActorStaffId);
            if (!actorStaffId.HasValue)
            {
                return BadRequest(new { message = "Thiếu actorStaffId hợp lệ." });
            }

            var updated = await _quotationWorkflowService.SubmitQuotationAsync(quotationId, actorStaffId.Value, request?.Note);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/insurance-approval")]
    public async Task<ActionResult<QuotationResponseDto>> ConfirmInsuranceApproval(
        long quotationId,
        [FromBody] QuotationInsuranceApprovalRequestDto request)
    {
        try
        {
            var normalizedRequest = request ?? new QuotationInsuranceApprovalRequestDto();
            if (!this.IsServiceAdvisorOrReceptionist())
            {
                return Forbid();
            }

            var actorStaffId = ResolveActorStaffId(normalizedRequest.ActorStaffId);
            if (!actorStaffId.HasValue)
            {
                return BadRequest(new { message = "Thiếu actorStaffId hợp lệ." });
            }

            normalizedRequest.ActorStaffId = actorStaffId.Value;
            var updated = await _quotationWorkflowService.ConfirmInsuranceApprovedAsync(quotationId, normalizedRequest);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/insurance-evidence")]
    public async Task<ActionResult<object>> UploadInsuranceEvidence(long quotationId, [FromForm] IFormFile file)
    {
        if (quotationId <= 0)
        {
            return BadRequest(new { message = "Mã báo giá không hợp lệ." });
        }

        if (file == null || file.Length <= 0)
        {
            return BadRequest(new { message = "Vui lòng chọn tệp bằng chứng." });
        }

        if (file.Length > MaxInsuranceEvidenceBytes)
        {
            return BadRequest(new { message = "Tệp bằng chứng vượt quá 10MB." });
        }

        var quotationExists = await _context.Quotations.AsNoTracking()
            .AnyAsync(x => x.Id == quotationId);
        if (!quotationExists)
        {
            return NotFound(new { message = "Không tìm thấy báo giá." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;
        var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        await using var stream = file.OpenReadStream();
        var evidenceUrl = await _cloudStorageService.UploadAsync(
            stream,
            $"quotations/{quotationId}/insurance-evidence",
            fileName,
            contentType,
            HttpContext.RequestAborted);

        return Ok(new { quotationId, insuranceApprovalEvidenceUrl = evidenceUrl });
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/send-to-customer")]
    public async Task<ActionResult<QuotationResponseDto>> SendToCustomer(
        long quotationId,
        [FromBody] QuotationStatusUpdateRequestDto request)
    {
        try
        {
            if (!this.IsServiceAdvisorOrReceptionist())
            {
                return Forbid();
            }

            var actorStaffId = ResolveActorStaffId(request?.ActorStaffId);
            if (!actorStaffId.HasValue)
            {
                return BadRequest(new { message = "Thiếu actorStaffId hợp lệ." });
            }

            var updated = await _quotationWorkflowService.SendQuotationToCustomerAsync(
                quotationId,
                actorStaffId.Value,
                request?.Note,
                request?.IdempotencyKey);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/publish-customer-link")]
    public async Task<ActionResult<QuotationCustomerLinkResponseDto>> PublishCustomerLink(
        long quotationId,
        [FromBody] QuotationCustomerLinkPublishRequestDto request)
    {
        try
        {
            var normalizedRequest = request ?? new QuotationCustomerLinkPublishRequestDto();
            if (!this.IsServiceAdvisorOrReceptionist())
            {
                return Forbid();
            }

            var actorStaffId = ResolveActorStaffId(normalizedRequest.ActorStaffId);
            if (!actorStaffId.HasValue)
            {
                return BadRequest(new { message = "Thiếu actorStaffId hợp lệ." });
            }

            normalizedRequest.ActorStaffId = actorStaffId.Value;
            var result = await _quotationWorkflowService.PublishQuotationCustomerLinkAsync(quotationId, normalizedRequest);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/{quotationId:long}/supersede")]
    public async Task<ActionResult<QuotationResponseDto>> Supersede(long quotationId, [FromBody] QuotationStatusUpdateRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.SupersedeQuotationAsync(quotationId, request?.ActorStaffId ?? 0, request?.Note);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private long? ResolveActorStaffId(long? requestActorStaffId)
    {
        if (requestActorStaffId.HasValue && requestActorStaffId.Value > 0)
        {
            return requestActorStaffId.Value;
        }

        return this.GetCurrentUserId();
    }

    [Authorize]
    [HttpGet("quotations/{quotationId:long}/export/excel")]
    public async Task<IActionResult> ExportQuotationExcel(long quotationId)
    {
        var quotation = await _context.Quotations
            .AsNoTracking()
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            return NotFound(new { message = "Không tìm thấy báo giá." });
        }

        var rows = quotation.QuotationDetails
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                ItemName = (x.ItemName ?? string.Empty).Replace('"', '\''),
                UnitPrice = x.UnitPriceAtTime
            })
            .ToList();

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("Tên hạng mục,Đơn giá xưởng đề xuất");
        foreach (var row in rows)
        {
            csvBuilder
                .Append('"')
                .Append(row.ItemName)
                .Append("\",")
                .Append(row.UnitPrice.ToString("0.##", CultureInfo.InvariantCulture))
                .AppendLine();
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(csvBuilder.ToString());
        var payload = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, payload, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, payload, preamble.Length, content.Length);

        var fileName = $"quotation_{quotation.Code}_{VietnamTime.Now:yyyyMMddHHmmss}.csv";
        return File(payload, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("quotations/public/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QuotationPublicViewResponseDto), 200)]
    public async Task<ActionResult<QuotationPublicViewResponseDto>> GetByPublicToken(string token)
    {
        try
        {
            var data = await _quotationWorkflowService.GetQuotationByPublicTokenAsync(token);
            return Ok(data);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/public/{token}/decision")]
    [AllowAnonymous]
    public async Task<ActionResult<QuotationResponseDto>> ApplyDecisionByPublicToken(
        string token,
        [FromBody] QuotationPublicDecisionRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.ApplyCustomerDecisionByPublicTokenAsync(token, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("quotations/public/{token}/otp/send")]
    [AllowAnonymous]
    public async Task<ActionResult<QuotationPublicOtpResponseDto>> SendDecisionOtpByPublicToken(
        string token,
        [FromBody] QuotationPublicOtpRequestDto? request)
    {
        try
        {
            var response = await _quotationWorkflowService.SendDecisionOtpByPublicTokenAsync(token, request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
