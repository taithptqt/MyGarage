using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Responses;
using MyGarage.Security;

namespace MyGarage.Controllers;

public partial class QuotationController : ControllerBase
{
    [Authorize(Roles = "Staff")]
    [HttpGet("quotations/{quotationId:long}/excel-exports")]
    public async Task<IActionResult> ExportQuotationExcel(long quotationId)
    {
        var result = await _commandFacade.ExportExcelAsync(quotationId, HttpContext.RequestAborted);
        if (result.StatusCode == 200 && result.Data != null)
        {
            return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
        }

        return result.StatusCode switch
        {
            404 => NotFound(result.ErrorPayload),
            400 => BadRequest(result.ErrorPayload),
            _ => StatusCode(result.StatusCode, result.ErrorPayload)
        };
    }

    [Authorize(Roles = "Staff")]
    [HttpPost("quotations/{quotationId:long}/excel-imports")]
    public async Task<ActionResult<QuotationResponseDto>> ImportQuotationExcel(
        long quotationId,
        IFormFile file,
        [FromForm] long? actorStaffId,
        [FromForm] string? note)
    {
        if (quotationId <= 0)
        {
            return this.BadRequestProblem("Mã báo giá không hợp lệ.");
        }

        var scan = await _fileSecurityScanner.ScanFormFileAsync(
            file,
            maxBytes: MaxInsuranceEvidenceBytes,
            cancellationToken: HttpContext.RequestAborted);
        if (!scan.IsClean)
        {
            return this.BadRequestProblem(scan.ErrorMessage ?? "Tệp không hợp lệ.");
        }

        var result = await _commandFacade.ImportExcelAsync(
            quotationId,
            file.FileName,
            scan.Bytes,
            actorStaffId,
            this.GetCurrentUserId(),
            note,
            HttpContext.RequestAborted);
        return ToActionResult(result);
    }
}
