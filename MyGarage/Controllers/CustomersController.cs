using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/customers")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly IMapper _mapper;
        private readonly GarageContext _context;

        public CustomersController(ICustomerService customerService, IMapper mapper, GarageContext context)
        {
            _customerService = customerService;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerResponseDto>>> GetAll([FromQuery] long? branchId = null)
        {
            IEnumerable<Customer> customers;
            if (!branchId.HasValue)
            {
                customers = await _customerService.GetAllAsync();
            }
            else
            {
                var preferredBranchTag = $"[PreferredBranchId:{branchId.Value}]";
                var customerIdsByBranch = _context.ServiceOrders
                    .Where(so => so.OriginBranchId == branchId.Value || so.ExecuteBranchId == branchId.Value)
                    .Select(so => so.CustomerId)
                    .Distinct();

                var customerIdsByAppointment = _context.Appointments
                    .Where(a => a.BranchId == branchId.Value)
                    .Select(a => a.CustomerId)
                    .Distinct();

                var customerIds = await customerIdsByBranch
                    .Union(customerIdsByAppointment)
                    .ToListAsync();

                customers = await _context.Customers
                    .Where(c =>
                        customerIds.Contains(c.Id) ||
                        (c.Note ?? string.Empty).Contains(preferredBranchTag))
                    .ToListAsync();
            }

            var dtos = _mapper.Map<IEnumerable<CustomerResponseDto>>(customers);
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<CustomerResponseDto>> GetById(long id)
        {
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
            var entity = _mapper.Map<Customer>(customerDto);
            var createdCustomer = await _customerService.AddAsync(entity);
            var responseDto = _mapper.Map<CustomerResponseDto>(createdCustomer);
            return CreatedAtAction(nameof(GetById), new { id = createdCustomer.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<CustomerResponseDto>> Update(long id, [FromBody] CustomerUpdateDto customerDto)
        {
            var existingCustomer = await _customerService.GetByIdAsync(id);
            if (existingCustomer == null)
            {
                return NotFound();
            }

            _mapper.Map(customerDto, existingCustomer);
            var updatedCustomer = await _customerService.UpdateAsync(id, existingCustomer);
            var responseDto = _mapper.Map<CustomerResponseDto>(updatedCustomer);
            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _customerService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
