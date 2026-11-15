using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Controllers
{
    [Route("api/v1/customers")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly ICustomerQueryService _customerQueryService;

        public CustomersController(ICustomerService customerService, ICustomerQueryService customerQueryService)
        {
            _customerService = customerService;
            _customerQueryService = customerQueryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<CustomerResponseDto>>> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranch, out var denied)) return (ActionResult)denied!;
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (items, total) = await _customerQueryService.GetScopedPageAsync(scopedBranch, normalizedPage, normalizedSize, HttpContext.RequestAborted);
            var dtos = _customerService.MapToDtos(items);
            return Ok(PagedEnvelope<CustomerResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<CustomerResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branch, out var denied)) return (ActionResult)denied!;
            if (!await _customerQueryService.ExistsInScopeAsync(id, branch, HttpContext.RequestAborted)) return NotFound();
            var dto = await _customerService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CustomerResponseDto>> Create([FromBody] CustomerCreateDto customerDto)
        {
            if (!this.TryResolveBranchScope(null, out var branch, out var denied)) return (ActionResult)denied!;
            var responseDto = await _customerService.CreateDtoAsync(customerDto, branch);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<CustomerResponseDto>> Update(long id, [FromBody] CustomerUpdateDto customerDto)
        {
            if (!this.TryResolveBranchScope(null, out var branch, out var denied)) return (ActionResult)denied!;
            if (!await _customerQueryService.ExistsInScopeAsync(id, branch, HttpContext.RequestAborted)) return NotFound();
            var responseDto = await _customerService.UpdateDtoAsync(id, customerDto, branch);
            if (responseDto == null)
            {
                return NotFound();
            }

            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branch, out var denied)) return denied!;
            if (!await _customerQueryService.ExistsInScopeAsync(id, branch, HttpContext.RequestAborted)) return NotFound();
            var result = await _customerService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
