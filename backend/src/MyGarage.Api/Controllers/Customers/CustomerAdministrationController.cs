using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/customers/admin-list")]
[Route("api/v1/customers/admin-list")]
public sealed class CustomerAdministrationController : ControllerBase
{
    private readonly IGarageContext _db;
    private readonly IMapper _mapper;

    public CustomerAdministrationController(IGarageContext db, IMapper mapper) { _db = db; _mapper = mapper; }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] long? branchId, [FromQuery] string? search,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied)) return denied!;
        var customers = _db.Customers.AsNoTracking().AsQueryable();
        if (scopedBranchId.HasValue)
        {
            var id = scopedBranchId.Value;
            var tag = $"[PreferredBranchId:{id}]";
            var fromOrders = _db.ServiceOrders.Where(x => x.OriginBranchId == id || x.ExecuteBranchId == id).Select(x => x.CustomerId);
            var fromAppointments = _db.Appointments.Where(x => x.BranchId == id).Select(x => x.CustomerId);
            customers = customers.Where(x => fromOrders.Contains(x.Id) || fromAppointments.Contains(x.Id) || (x.Note ?? "").Contains(tag));
        }
        var totalCustomers = await customers.CountAsync(cancellationToken);
        var activeCustomers = await customers.CountAsync(x => x.IsActive == true, cancellationToken);

        // Legacy view chooses the last current-owner relation, or the first relation if none is current.
        var query = customers.Select(c => new
        {
            Customer = c,
            TierName = c.Tier != null ? c.Tier.TierName : null,
            Primary = c.CustomerVehicles.Where(link => link.IsDeleted != true)
                .OrderByDescending(link => link.IsCurrentOwner == true)
                .ThenBy(link => link.IsCurrentOwner == true ? -link.Id : link.Id)
                .Select(link => new
                {
                    LinkId = link.Id,
                    VehicleId = link.VehicleId,
                    Exists = link.Vehicle.IsDeleted != true,
                    Plate = link.Vehicle.LicensePlate,
                    Color = link.Vehicle.Color,
                    Year = link.Vehicle.ManufactureYear,
                    ModelId = link.Vehicle.ModelId,
                    ModelName = link.Vehicle.Model.ModelName,
                    BrandId = link.Vehicle.Model.BrandId,
                    BrandName = link.Vehicle.Model.Brand.Name
                }).FirstOrDefault()
        });
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => (x.Customer.FullName ?? "").ToLower().Contains(term) ||
                (x.Customer.Email ?? "").ToLower().Contains(term) || (x.Customer.Phone ?? "").ToLower().Contains(term) ||
                (x.TierName ?? "-").ToLower().Contains(term) ||
                ((x.Primary != null && x.Primary.Exists ? x.Primary.Plate : "-") ?? "-").ToLower().Contains(term) ||
                ((x.Primary != null && x.Primary.Exists ? x.Primary.BrandName : "-") ?? "-").ToLower().Contains(term) ||
                ((x.Primary != null && x.Primary.Exists ? x.Primary.ModelName : "-") ?? "-").ToLower().Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var offset = ((long)currentPage - 1) * size;
        var rows = await query.OrderBy(x => x.Customer.Id)
            .Skip(offset >= total ? total : (int)offset).Take(size).ToListAsync(cancellationToken);
        var items = rows.Select(x => new
        {
            customer = _mapper.Map<CustomerResponseDto>(x.Customer),
            tierName = x.TierName ?? "-",
            customerVehicleId = x.Primary?.LinkId,
            vehicleId = x.Primary?.VehicleId,
            brandId = x.Primary?.Exists == true ? x.Primary.BrandId : (long?)null,
            brandName = x.Primary?.Exists == true ? x.Primary.BrandName : null,
            modelId = x.Primary?.Exists == true ? x.Primary.ModelId : (long?)null,
            modelName = x.Primary?.Exists == true ? x.Primary.ModelName : null,
            licensePlate = x.Primary?.Exists == true ? x.Primary.Plate : null,
            color = x.Primary?.Exists == true ? x.Primary.Color : null,
            manufactureYear = x.Primary?.Exists == true ? x.Primary.Year : null
        });
        var envelope = PaginationExtensions.CreatePagedEnvelope(items, total, currentPage, size);
        return Ok(new
        {
            items = envelope.Items,
            total = envelope.Total,
            totalCount = envelope.TotalCount,
            page = envelope.Page,
            pageSize = envelope.PageSize,
            totalPages = envelope.TotalPages,
            hasPreviousPage = envelope.HasPreviousPage,
            hasNextPage = envelope.HasNextPage,
            totalCustomers,
            activeCustomers
        });
    }
}
