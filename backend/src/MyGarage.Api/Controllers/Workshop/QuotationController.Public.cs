using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Application.Authorization;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Security;

namespace MyGarage.Controllers;

public partial class QuotationController : ControllerBase
{
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
            return this.BadRequestProblem(ex.Message);
        }
    }

    [HttpPost("quotations/public/{token}/decisions")]
    [AllowAnonymous]
    [EnableRateLimiting("otp")]
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
            return this.BadRequestProblem(ex.Message);
        }
    }

    [HttpPost("quotations/public/{token}/otp-requests")]
    [AllowAnonymous]
    [EnableRateLimiting("otp")]
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
            return this.BadRequestProblem(ex.Message);
        }
    }
}
