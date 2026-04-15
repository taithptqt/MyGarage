using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[Route("api")]
[ApiController]
public class QuotationController : ControllerBase
{
    private readonly IQuotationWorkflowService _quotationWorkflowService;

    public QuotationController(IQuotationWorkflowService quotationWorkflowService)
    {
        _quotationWorkflowService = quotationWorkflowService;
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

    [HttpPost("quotations/{quotationId:long}/submit")]
    public async Task<ActionResult<QuotationResponseDto>> Submit(long quotationId, [FromBody] QuotationStatusUpdateRequestDto request)
    {
        try
        {
            var updated = await _quotationWorkflowService.SubmitQuotationAsync(quotationId, request?.ActorStaffId ?? 0, request?.Note);
            return Ok(updated);
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
}
