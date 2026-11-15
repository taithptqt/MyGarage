using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Authorization;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Security;

namespace MyGarage.Controllers;

[Route("api/v1")]
[ApiController]
public partial class QuotationController : ControllerBase
{
    private readonly IQuotationWorkflowService _quotationWorkflowService;
    private readonly IQuotationCommandFacade _commandFacade;
    private readonly IFileSecurityScanner _fileSecurityScanner;
    private readonly IQuotationAuthorizationService _quotationAuthorizationService;
    private const long MaxInsuranceEvidenceBytes = 10 * 1024 * 1024;

    public QuotationController(
        IQuotationWorkflowService quotationWorkflowService,
        IQuotationCommandFacade commandFacade,
        IFileSecurityScanner fileSecurityScanner,
        IQuotationAuthorizationService quotationAuthorizationService)
    {
        _quotationWorkflowService = quotationWorkflowService;
        _commandFacade = commandFacade;
        _fileSecurityScanner = fileSecurityScanner;
        _quotationAuthorizationService = quotationAuthorizationService;
    }

    [Authorize(Roles = "Staff")]
    [HttpPost("service-orders/{serviceOrderId:long}/quotations")]
    public Task<ActionResult<QuotationResponseDto>> CreateQuotation(
        long serviceOrderId,
        [FromBody] QuotationCreateRequestDto request)
        => ExecuteWorkflowAsync(
            () => _quotationWorkflowService.CreateQuotationAsync(serviceOrderId, request),
            created => CreatedAtAction(nameof(GetById), new { quotationId = created.Id }, created));

    [Authorize(Roles = "Staff")]
    [HttpGet("service-orders/{serviceOrderId:long}/quotations")]
    public async Task<ActionResult> GetByServiceOrder(long serviceOrderId)
    {
        var rows = await _quotationWorkflowService.GetQuotationsByServiceOrderAsync(serviceOrderId);
        return Ok(rows);
    }

    [Authorize(Roles = "Staff")]
    [HttpGet("service-orders/{serviceOrderId:long}/mandatory-overrides")]
    public async Task<ActionResult> GetMandatoryOverridesByServiceOrder(long serviceOrderId)
    {
        var rows = await _quotationWorkflowService.GetMandatoryOverrideRequestsByServiceOrderAsync(serviceOrderId);
        return Ok(rows);
    }

    [Authorize]
    [HttpGet("quotations/{quotationId:long}")]
    public async Task<ActionResult<QuotationResponseDto>> GetById(long quotationId)
    {
        var row = await _quotationWorkflowService.GetQuotationAsync(quotationId);
        if (row == null)
        {
            return this.NotFoundProblem("Không tìm thấy báo giá.");
        }

        var actor = this.ToUserActorContext();
        if (!await _quotationAuthorizationService.CanAccessQuotationAsync(quotationId, actor))
        {
            return Forbid();
        }

        return Ok(row);
    }

    [Authorize]
    [HttpGet("quotations/{quotationId:long}/discounts")]
    public async Task<ActionResult<IReadOnlyList<QuotationDiscountResponseDto>>> GetQuotationDiscounts(long quotationId)
    {
        try
        {
            var actor = this.ToUserActorContext();
            if (!await _quotationAuthorizationService.CanAccessQuotationDiscountsAsync(quotationId, actor))
            {
                return Forbid();
            }

            var rows = await _quotationWorkflowService.GetQuotationDiscountsAsync(quotationId);
            return Ok(rows);
        }
        catch (InvalidOperationException ex)
        {
            return this.BadRequestProblem(ex.Message);
        }
    }

    [Authorize]
    [HttpPut("quotations/{quotationId:long}/discounts")]
    public async Task<ActionResult<IReadOnlyList<QuotationDiscountResponseDto>>> UpsertQuotationDiscounts(
        long quotationId,
        [FromBody] QuotationDiscountUpsertRequestDto request)
    {
        var result = await _commandFacade.UpsertDiscountsAsync(quotationId, request, this.ToUserActorContext());
        return ToActionResult(result);
    }

    [Authorize(Roles = "Staff")]
    [HttpPost("quotations/{quotationId:long}/approvals")]
    public Task<ActionResult<QuotationResponseDto>> Approve(long quotationId, [FromBody] QuotationApproveRequestDto request)
        => ExecuteWorkflowAsync(() => _quotationWorkflowService.ApproveQuotationAsync(quotationId, request));

    [Authorize(Roles = "Staff")]
    [HttpPost("quotations/{quotationId:long}/financial-approvals")]
    public async Task<ActionResult<QuotationResponseDto>> FinancialApprove(long quotationId, [FromBody] QuotationFinancialApprovalRequestDto request)
    {
        var result = await _commandFacade.FinancialApproveAsync(quotationId, request, this.GetCurrentUserId());
        return ToActionResult(result);
    }

    [Authorize(Roles = "Staff")]
    [HttpGet("branches/{branchId:long}/quotation-approval-policy")]
    public Task<ActionResult<BranchQuotationApprovalPolicyResponseDto>> GetBranchQuotationApprovalPolicy(long branchId)
        => ExecuteWorkflowAsync(() => _quotationWorkflowService.GetBranchQuotationApprovalPolicyAsync(branchId));

    [Authorize(Roles = "Staff")]
    [HttpPut("branches/{branchId:long}/quotation-approval-policy")]
    public async Task<ActionResult<BranchQuotationApprovalPolicyResponseDto>> UpsertBranchQuotationApprovalPolicy(
        long branchId,
        [FromBody] BranchQuotationApprovalPolicyUpsertRequestDto request)
    {
        var result = await _commandFacade.UpsertBranchApprovalPolicyAsync(branchId, request, this.GetCurrentUserId());
        return ToActionResult(result);
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/customer-decisions")]
    public async Task<ActionResult<QuotationResponseDto>> ApplyCustomerDecision(
        long quotationId,
        [FromBody] QuotationCustomerDecisionRequestDto request)
    {
        var result = await _commandFacade.ApplyCustomerDecisionAsync(
            quotationId,
            request,
            this.GetCurrentUserId(),
            this.IsStaffUser(),
            HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [Authorize(Roles = "Staff")]
    [HttpPost("quotations/{quotationId:long}/clones")]
    public Task<ActionResult<QuotationResponseDto>> CloneQuotation(
        long quotationId,
        [FromBody] QuotationCloneRequestDto request)
        => ExecuteWorkflowAsync(() => _quotationWorkflowService.CloneQuotationAsync(quotationId, request));

    [Authorize(Roles = "Staff")]
    [HttpPost("quotations/{quotationId:long}/details/{quotationDetailId:long}/cancellations")]
    public Task<ActionResult<QuotationResponseDto>> CancelQuotationDetail(
        long quotationId,
        long quotationDetailId,
        [FromBody] QuotationDetailCancelRequestDto request)
        => ExecuteWorkflowAsync(() => _quotationWorkflowService.CancelQuotationDetailAsync(quotationId, quotationDetailId, request));

    [Authorize(Roles = "Staff")]
    [HttpPost("quotations/{quotationId:long}/split-billings")]
    public Task<ActionResult<QuotationResponseDto>> UpdateSplitBilling(
        long quotationId,
        [FromBody] QuotationSplitBillingUpdateRequestDto request)
        => ExecuteWorkflowAsync(() => _quotationWorkflowService.UpdateSplitBillingAsync(quotationId, request));

    [Authorize(Roles = "Staff")]
    [HttpPost("mandatory-overrides/{requestId:long}/decisions")]
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
            return this.BadRequestProblem(ex.Message);
        }
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/internal-submissions")]
    public async Task<ActionResult<QuotationResponseDto>> SubmitInternalApproval(long quotationId, [FromBody] QuotationStatusUpdateRequestDto request)
    {
        var result = await _commandFacade.SubmitInternalApprovalAsync(
            quotationId,
            request,
            this.GetCurrentUserId(),
            this.IsServiceAdvisorOrReceptionist());
        return ToActionResult(result);
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/submissions")]
    public async Task<ActionResult<QuotationResponseDto>> Submit(long quotationId, [FromBody] QuotationStatusUpdateRequestDto request)
    {
        var result = await _commandFacade.SubmitAsync(
            quotationId,
            request,
            this.GetCurrentUserId(),
            this.IsServiceAdvisorOrReceptionist());
        return ToActionResult(result);
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/insurance-approvals")]
    public async Task<ActionResult<QuotationResponseDto>> ConfirmInsuranceApproval(
        long quotationId,
        [FromBody] QuotationInsuranceApprovalRequestDto request)
    {
        var result = await _commandFacade.ConfirmInsuranceApprovalAsync(
            quotationId,
            request,
            this.GetCurrentUserId(),
            this.IsServiceAdvisorOrReceptionist());
        return ToActionResult(result);
    }

    [Authorize(Roles = "Staff")]
    [HttpPost("quotations/{quotationId:long}/insurance-evidence")]
    public async Task<ActionResult<object>> UploadInsuranceEvidence(long quotationId, IFormFile file)
    {
        if (quotationId <= 0)
        {
            return this.BadRequestProblem("Mã báo giá không hợp lệ.");
        }

        if (file == null || file.Length <= 0)
        {
            return this.BadRequestProblem("Vui lòng chọn tệp bằng chứng.");
        }

        if (file.Length > MaxInsuranceEvidenceBytes)
        {
            return this.BadRequestProblem("Tệp bằng chứng vượt quá 10MB.");
        }

        var scan = await _fileSecurityScanner.ScanFormFileAsync(
            file,
            maxBytes: MaxInsuranceEvidenceBytes,
            cancellationToken: HttpContext.RequestAborted);
        if (!scan.IsClean)
        {
            return this.BadRequestProblem(scan.ErrorMessage ?? "Tệp không hợp lệ.");
        }

        var result = await _commandFacade.UploadInsuranceEvidenceAsync(
            quotationId,
            file.FileName,
            file.ContentType,
            scan.Bytes,
            HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [Authorize(Roles = "Staff")]
    [HttpDelete("quotations/{quotationId:long}/insurance-evidence")]
    public async Task<ActionResult> DeleteInsuranceEvidence(long quotationId)
    {
        var result = await _commandFacade.DeleteInsuranceEvidenceAsync(quotationId, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/customer-dispatches")]
    public async Task<ActionResult<QuotationResponseDto>> SendToCustomer(
        long quotationId,
        [FromBody] QuotationStatusUpdateRequestDto request)
    {
        var result = await _commandFacade.SendToCustomerAsync(
            quotationId,
            request,
            this.GetCurrentUserId(),
            this.IsServiceAdvisorOrReceptionist());
        return ToActionResult(result);
    }

    [Authorize]
    [HttpPost("quotations/{quotationId:long}/customer-links")]
    public async Task<ActionResult<QuotationCustomerLinkResponseDto>> PublishCustomerLink(
        long quotationId,
        [FromBody] QuotationCustomerLinkPublishRequestDto request)
    {
        var result = await _commandFacade.PublishCustomerLinkAsync(
            quotationId,
            request,
            this.GetCurrentUserId(),
            this.IsServiceAdvisorOrReceptionist());
        return ToActionResult(result);
    }

    [Authorize(Roles = "Staff")]
    [HttpPost("quotations/{quotationId:long}/supersessions")]
    public Task<ActionResult<QuotationResponseDto>> Supersede(long quotationId, [FromBody] QuotationStatusUpdateRequestDto request)
        => ExecuteWorkflowAsync(
            () => _quotationWorkflowService.SupersedeQuotationAsync(quotationId, request?.ActorStaffId ?? 0, request?.Note),
            created => CreatedAtAction(nameof(GetById), new { quotationId = created.Id }, created));

    private async Task<ActionResult<T>> ExecuteWorkflowAsync<T>(
        Func<Task<T>> action,
        Func<T, ActionResult<T>>? onSuccess = null)
    {
        try
        {
            var data = await action();
            return onSuccess != null ? onSuccess(data) : Ok(data);
        }
        catch (InvalidOperationException ex)
        {
            return this.BadRequestProblem(ex.Message);
        }
    }

    private ActionResult<T> ToActionResult<T>(QuotationCommandResult<T> result)
    {
        return result.StatusCode switch
        {
            200 => Ok(result.Data),
            401 => Unauthorized(),
            403 => Forbid(),
            404 => result.ErrorPayload == null ? NotFound() : NotFound(result.ErrorPayload),
            400 => BadRequest(result.ErrorPayload),
            _ => StatusCode(result.StatusCode, result.ErrorPayload ?? result.Data)
        };
    }

    private ActionResult ToActionResult(QuotationCommandResult result)
    {
        return result.StatusCode switch
        {
            200 => Ok(result.Payload),
            401 => Unauthorized(),
            403 => Forbid(),
            404 => result.Payload == null ? NotFound() : NotFound(result.Payload),
            400 => BadRequest(result.Payload),
            _ => StatusCode(result.StatusCode, result.Payload)
        };
    }
}
