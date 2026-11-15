using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Customers;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class CustomerService : GenericService<Customer>, ICustomerService
    {
        private readonly ICustomerStore _store;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IMapper _mapper;

        public CustomerService(ICustomerRepository repository, ICustomerStore store, IPasswordHasher passwordHasher, IMapper mapper) : base(repository)
        {
            _store = store;
            _passwordHasher = passwordHasher;
            _mapper = mapper;
        }

        public override async System.Threading.Tasks.Task<Customer> AddAsync(Customer entity)
        {
            if (!string.IsNullOrEmpty(entity.PasswordHash))
            {
                entity.PasswordHash = _passwordHasher.HashPassword(entity.PasswordHash);
            }
            return await base.AddAsync(entity);
        }

        public override async System.Threading.Tasks.Task<Customer?> UpdateAsync(object id, Customer entity)
        {
            if (!string.IsNullOrEmpty(entity.PasswordHash) && !entity.PasswordHash.StartsWith("$2a$") && !entity.PasswordHash.StartsWith("$2b$") && !entity.PasswordHash.StartsWith("$2y$"))
            {
                // Only hash if it looks like a raw password and not already a bcrypt hash
                entity.PasswordHash = _passwordHasher.HashPassword(entity.PasswordHash);
            }
            return await base.UpdateAsync(id, entity);
        }

        public async Task<IEnumerable<Customer>> GetByBranchIdAsync(long branchId)
        {
            return await _store.GetCustomersByBranchIdAsync(branchId);
        }

        public CustomerResponseDto MapToDto(Customer customer)
        {
            return _mapper.Map<CustomerResponseDto>(customer);
        }

        public IEnumerable<CustomerResponseDto> MapToDtos(IEnumerable<Customer> items)
        {
            return _mapper.Map<IEnumerable<CustomerResponseDto>>(items);
        }

        public async Task<CustomerResponseDto?> GetDtoByIdAsync(long id)
        {
            var customer = await GetByIdAsync(id);
            return customer == null ? null : _mapper.Map<CustomerResponseDto>(customer);
        }

        public async Task<CustomerResponseDto> CreateDtoAsync(CustomerCreateDto customerDto, long? branchId = null)
        {
            var entity = _mapper.Map<Customer>(customerDto);
            entity.Type ??= CustomerDomainRules.TypeGuest;
            if (branchId.HasValue)
            {
                entity.Note = CustomerDomainRules.SetPreferredBranchTag(entity.Note, branchId.Value);
            }

            var createdCustomer = await AddAsync(entity);
            return _mapper.Map<CustomerResponseDto>(createdCustomer);
        }

        public async Task<CustomerResponseDto?> UpdateDtoAsync(long id, CustomerUpdateDto customerDto, long? branchId = null)
        {
            var existingCustomer = await GetByIdAsync(id);
            if (existingCustomer == null)
            {
                return null;
            }

            var preferredBranch = CustomerDomainRules.ExtractPreferredBranchId(existingCustomer.Note);
            _mapper.Map(customerDto, existingCustomer);
            if (branchId.HasValue)
            {
                existingCustomer.Note = preferredBranch.HasValue
                    ? CustomerDomainRules.SetPreferredBranchTag(existingCustomer.Note, preferredBranch.Value)
                    : CustomerDomainRules.RemovePreferredBranchTag(existingCustomer.Note);
            }

            var updatedCustomer = await UpdateAsync(id, existingCustomer);
            return updatedCustomer == null ? null : _mapper.Map<CustomerResponseDto>(updatedCustomer);
        }
    }
}
