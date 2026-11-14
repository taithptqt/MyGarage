using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Domain.Rules;

namespace MyGarage.Controllers
{
    [Route("api/customers")]
    [Route("api/v1/customers")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public CustomersController(ICustomerService customerService, IMapper mapper, IGarageContext context)
        {
            _customerService = customerService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<CustomerResponseDto>>> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranch, out var denied)) return (ActionResult)denied!;
            var query = ScopedCustomers(scopedBranch);

            var envelope = await query
                .OrderBy(c => c.Id)
                .ToPagedEnvelopeAsync(page, pageSize, paged => _mapper.Map<IEnumerable<CustomerResponseDto>>(paged));
            return Ok(envelope);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<CustomerResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branch, out var denied)) return (ActionResult)denied!;
            if (!await ScopedCustomers(branch).AnyAsync(x => x.Id == id)) return NotFound();
            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<CustomerResponseDto>(customer);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CustomerResponseDto>> Create([FromBody] CustomerCreateDto customerDto)
        {
            if (!this.TryResolveBranchScope(null, out var branch, out var denied)) return (ActionResult)denied!;
            var entity = _mapper.Map<Customer>(customerDto);
            entity.Type ??= CustomerDomainRules.TypeGuest;
            if (branch.HasValue) entity.Note = CustomerDomainRules.SetPreferredBranchTag(entity.Note, branch.Value);
            var createdCustomer = await _customerService.AddAsync(entity);
            var responseDto = _mapper.Map<CustomerResponseDto>(createdCustomer);
            return CreatedAtAction(nameof(GetById), new { id = createdCustomer.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<CustomerResponseDto>> Update(long id, [FromBody] CustomerUpdateDto customerDto)
        {
            if (!this.TryResolveBranchScope(null, out var branch, out var denied)) return (ActionResult)denied!;
            if (!await ScopedCustomers(branch).AnyAsync(x => x.Id == id)) return NotFound();
            var existingCustomer = await _customerService.GetByIdAsync(id);
            if (existingCustomer == null)
            {
                return NotFound();
            }

            var preferredBranch = CustomerDomainRules.ExtractPreferredBranchId(existingCustomer.Note);
            _mapper.Map(customerDto, existingCustomer);
            if (branch.HasValue)
                existingCustomer.Note = preferredBranch.HasValue
                    ? CustomerDomainRules.SetPreferredBranchTag(existingCustomer.Note, preferredBranch.Value)
                    : CustomerDomainRules.RemovePreferredBranchTag(existingCustomer.Note);
            var updatedCustomer = await _customerService.UpdateAsync(id, existingCustomer);
            var responseDto = _mapper.Map<CustomerResponseDto>(updatedCustomer);
            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branch, out var denied)) return denied!;
            if (!await ScopedCustomers(branch).AnyAsync(x => x.Id == id)) return NotFound();
            var result = await _customerService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        private IQueryable<Customer> ScopedCustomers(long? branchId)
        {
            var query = _context.Customers.AsNoTracking().AsQueryable();
            if (!branchId.HasValue) return query;
            var id = branchId.Value;
            var tag = $"[PreferredBranchId:{id}]";
            var orderIds = _context.ServiceOrders.Where(x => x.OriginBranchId == id || x.ExecuteBranchId == id).Select(x => x.CustomerId);
            var appointmentIds = _context.Appointments.Where(x => x.BranchId == id).Select(x => x.CustomerId);
            return query.Where(x => orderIds.Contains(x.Id) || appointmentIds.Contains(x.Id) || (x.Note ?? "").Contains(tag));
        }
    }
}
