using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/service-boms")]
[Route("api/v1/service-boms")]
public class ServiceBomsController : ControllerBase
{
    private readonly GarageContext _context;

    public ServiceBomsController(GarageContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceBomResponseDto>>> GetAll(
        [FromQuery] long serviceId,
        [FromQuery] long? vehicleModelId = null)
    {
        if (serviceId <= 0 || !vehicleModelId.HasValue || vehicleModelId.Value <= 0)
        {
            return Ok(Array.Empty<ServiceBomResponseDto>());
        }

        var query = _context.ServiceBoms
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId && x.VehicleModelId == vehicleModelId.Value);

        var rows = await query
            .Select(x => new ServiceBomResponseDto
            {
                Id = x.Id,
                ServiceId = x.ServiceId,
                VehicleModelId = x.VehicleModelId,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Quantity = x.Quantity,
                Unit = x.Unit,
                IsMandatory = x.IsMandatory,
            })
            .ToListAsync();

        return Ok(rows);
    }
}
