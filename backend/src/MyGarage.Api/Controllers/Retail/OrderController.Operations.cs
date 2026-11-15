using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers;

public partial class OrderController : ControllerBase
{
    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> Get(int id)
    {
        try
        {
            var result = await _orderCommandFacade.GetOrderWithSyncAsync(
                id,
                this.GetCurrentUserId(),
                this.IsStaffUser(),
                HttpContext.RequestAborted);
            if (result.StatusCode != 200 || result.Data == null)
            {
                return MapOrderCommandError(result);
            }

            return Ok(result.Data);
        }
        catch (Exception ex)
        {
            return this.ProblemMessage(
                StatusCodes.Status500InternalServerError,
                $"Failed to retrieve order {id}",
                extensions: new Dictionary<string, object?> { ["error"] = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("{orderId}/cancellations")]
    [HttpPatch("{orderId}/status")]
    public async Task<ActionResult<PayOsLinkSnapshot>> CancelPayment(int orderId, string cancellationReason)
    {
        try
        {
            var result = await _orderCommandFacade.CancelPaymentAsync(
                orderId,
                cancellationReason,
                this.GetCurrentUserId(),
                this.IsStaffUser(),
                HttpContext.RequestAborted);
            if (result.StatusCode != 200 || result.Data == null)
            {
                return MapOrderCommandError(result);
            }

            return Ok(result.Data);
        }
        catch (Exception ex)
        {
            return this.ProblemMessage(
                StatusCodes.Status500InternalServerError,
                $"Failed to cancel order {orderId}",
                extensions: new Dictionary<string, object?> { ["error"] = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("{orderId}/invoices")]
    public async Task<ActionResult<OrderInvoicesInfo>> GetInvoices(int orderId)
    {
        try
        {
            var result = await _orderCommandFacade.GetInvoicesWithSyncAsync(
                orderId,
                this.GetCurrentUserId(),
                this.IsStaffUser(),
                HttpContext.RequestAborted);
            if (result.StatusCode != 200 || result.Data == null)
            {
                return MapOrderCommandError(result);
            }

            return Ok(result.Data);
        }
        catch (Exception ex)
        {
            return this.ProblemMessage(
                StatusCodes.Status500InternalServerError,
                $"Failed to retrieve invoice for order {orderId}",
                extensions: new Dictionary<string, object?> { ["error"] = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("{orderId}/invoices/{invoiceId}/download")]
    [HttpGet("{orderId}/invoices/{invoiceId}")]
    public async Task<ActionResult> DownloadInvoice(int orderId, string invoiceId)
    {
        try
        {
            var result = await _orderCommandFacade.DownloadInvoiceAsync(
                orderId,
                invoiceId,
                this.GetCurrentUserId(),
                this.IsStaffUser(),
                HttpContext.RequestAborted);
            if (result.StatusCode != 200 || result.Data == null)
            {
                return MapOrderCommandStatusError(result.StatusCode, result.ErrorPayload);
            }

            return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
        }
        catch (Exception ex)
        {
            return this.ProblemMessage(
                StatusCodes.Status500InternalServerError,
                $"Failed to download invoice {invoiceId}",
                extensions: new Dictionary<string, object?> { ["error"] = ex.Message });
        }
    }
}
