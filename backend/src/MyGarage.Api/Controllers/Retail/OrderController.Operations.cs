using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Interfaces;

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
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve order {OrderId}", id);
            return this.ProblemMessage(
                StatusCodes.Status500InternalServerError,
                $"Failed to retrieve order {id}");
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
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order {OrderId}", orderId);
            return this.ProblemMessage(
                StatusCodes.Status500InternalServerError,
                $"Failed to cancel order {orderId}");
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
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve invoices for order {OrderId}", orderId);
            return this.ProblemMessage(
                StatusCodes.Status500InternalServerError,
                $"Failed to retrieve invoice for order {orderId}");
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
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download invoice for order {OrderId}", orderId);
            return this.ProblemMessage(
                StatusCodes.Status500InternalServerError,
                "Failed to download invoice");
        }
    }
}
