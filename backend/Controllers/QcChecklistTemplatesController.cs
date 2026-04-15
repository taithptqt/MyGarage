using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[Route("api/qc-checklist-templates")]
[ApiController]
public sealed class QcChecklistTemplatesController : ControllerBase
{
    private readonly IGarageContext _db;

    public QcChecklistTemplatesController(IGarageContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplates([FromQuery] string? serviceType = null)
    {
        var normalizedServiceType = (serviceType ?? string.Empty).Trim();

        var query = _db.QcChecklistTemplates
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(normalizedServiceType))
        {
            var key = normalizedServiceType.ToUpperInvariant();
            query = query.Where(x =>
                string.IsNullOrWhiteSpace(x.ServiceCategory) ||
                x.ServiceCategory!.ToUpper() == key);
        }

        var rows = await query
            .OrderBy(x => string.IsNullOrWhiteSpace(x.ServiceCategory) ? 1 : 0)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                serviceCategory = x.ServiceCategory,
                isActive = x.IsActive,
                createdAt = x.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            serviceType = normalizedServiceType,
            total = rows.Count,
            items = rows
        });
    }
}

