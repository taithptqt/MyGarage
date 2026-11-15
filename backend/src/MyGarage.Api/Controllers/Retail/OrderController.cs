using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[ApiController]
[Route("api/v1/orders")]
public partial class OrderController(
    IOrderCommandFacade orderCommandFacade) : ControllerBase
{
    private readonly IOrderCommandFacade _orderCommandFacade = orderCommandFacade;

    private ActionResult MapOrderCommandStatusError(int statusCode, object? errorPayload) => statusCode switch
    {
        401 => Unauthorized(),
        403 => Forbid(),
        404 => errorPayload == null ? NotFound() : NotFound(errorPayload),
        _ => BadRequest(errorPayload)
    };

    private ActionResult<T> MapOrderCommandError<T>(OrderCommandResult<T> result) =>
        MapOrderCommandStatusError(result.StatusCode, result.ErrorPayload);

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Order>> CreatePayment(OrderCreateRequest request)
    {
        if (request == null)
        {
            return this.BadRequestProblem("Order data is required");
        }
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _orderCommandFacade.CreatePaymentAsync(
                request,
                this.GetCurrentUserId(),
                this.IsStaffUser(),
                HttpContext.RequestAborted);
            if (result.StatusCode != 201 || result.Data == null)
            {
                return MapOrderCommandError(result);
            }

            return CreatedAtAction(nameof(Get), new { id = result.Data.Id }, result.Data);
        }
        catch (Exception ex)
        {
            return this.ProblemMessage(
                StatusCodes.Status500InternalServerError,
                "Failed to create order",
                extensions: new Dictionary<string, object?> { ["error"] = ex.Message });
        }
    }
}
