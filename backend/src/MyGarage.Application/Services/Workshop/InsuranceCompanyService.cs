using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InsuranceCompanyService : GenericService<InsuranceCompany>, IInsuranceCompanyService
    {
        private readonly IMapper _mapper;

        public InsuranceCompanyService(IInsuranceCompanyRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<InsuranceCompanyResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<InsuranceCompanyResponseDto>>(items);
        }

        public async Task<InsuranceCompanyResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<InsuranceCompanyResponseDto>(item);
        }

        public async Task<InsuranceCompanyResponseDto> CreateDtoAsync(InsuranceCompanyCreateDto itemDto)
        {
            var entity = _mapper.Map<InsuranceCompany>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<InsuranceCompanyResponseDto>(createdItem);
        }

        public async Task<InsuranceCompanyResponseDto?> UpdateDtoAsync(long id, InsuranceCompanyUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<InsuranceCompanyResponseDto>(updatedItem);
        }
    }
}
