using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class SalaryCoefficientService : GenericService<SalaryCoefficient>, ISalaryCoefficientService
    {
        private static readonly HashSet<int> AllowedTenureThresholds = new()
        {
            0, 5, 10, 15, 20, 25
        };

        private readonly ISalaryCoefficientRepository _salaryCoefficientRepository;
        private readonly IMapper _mapper;

        public SalaryCoefficientService(ISalaryCoefficientRepository repository, IMapper mapper) : base(repository)
        {
            _salaryCoefficientRepository = repository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<SalaryCoefficientResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<SalaryCoefficientResponseDto>>(items);
        }

        public async Task<SalaryCoefficientResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<SalaryCoefficientResponseDto>(item);
        }

        public async Task<SalaryCoefficientResponseDto> CreateDtoAsync(SalaryCoefficientCreateDto itemDto)
        {
            var entity = _mapper.Map<SalaryCoefficient>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<SalaryCoefficientResponseDto>(createdItem);
        }

        public async Task<SalaryCoefficientResponseDto?> UpdateDtoAsync(long id, SalaryCoefficientUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<SalaryCoefficientResponseDto>(updatedItem);
        }

        public override async Task<IEnumerable<SalaryCoefficient>> GetAllAsync()
        {
            return await _salaryCoefficientRepository.GetAllConfiguredAsync();
        }

        public override async Task<SalaryCoefficient?> GetByIdAsync(object id)
        {
            if (id is not long salaryCoefficientId)
            {
                return null;
            }

            return await _salaryCoefficientRepository.GetConfiguredByIdAsync(salaryCoefficientId);
        }

        public override async Task<SalaryCoefficient> AddAsync(SalaryCoefficient entity)
        {
            var prepared = await PrepareEntityAsync(entity, null);
            return await base.AddAsync(prepared);
        }

        public override async Task<SalaryCoefficient?> UpdateAsync(object id, SalaryCoefficient entity)
        {
            if (id is not long salaryCoefficientId)
            {
                return null;
            }

            var existing = await _salaryCoefficientRepository.GetConfiguredByIdAsync(salaryCoefficientId);
            if (existing == null)
            {
                return null;
            }

            var prepared = await PrepareEntityAsync(entity, salaryCoefficientId);
            return await base.UpdateAsync(id, prepared);
        }

        private async Task<SalaryCoefficient> PrepareEntityAsync(SalaryCoefficient entity, long? excludeId)
        {
            if (!entity.YearsOfService.HasValue || entity.YearsOfService.Value < 0)
            {
                throw new InvalidOperationException("YearsOfService is required and must be greater than or equal to 0.");
            }

            if (entity.CoefficientValue < 0)
            {
                throw new InvalidOperationException("CoefficientValue must be greater than or equal to 0.");
            }

            var yearsOfService = entity.YearsOfService.Value;
            if (!AllowedTenureThresholds.Contains(yearsOfService))
            {
                throw new InvalidOperationException("YearsOfService must be one of these thresholds: 0, 5, 10, 15, 20, 25.");
            }

            var hasDuplicate = await _salaryCoefficientRepository.ExistsByYearsOfServiceAsync(yearsOfService, excludeId);
            if (hasDuplicate)
            {
                throw new InvalidOperationException($"A salary coefficient for {yearsOfService} year(s) already exists.");
            }

            entity.YearsOfService = yearsOfService;
            entity.CoefficientName = BuildCoefficientName(yearsOfService);
            entity.IsActive ??= true;
            entity.IsDeleted ??= false;
            entity.UpdatedAt = VietnamTime.Now;

            if (!excludeId.HasValue)
            {
                entity.CreatedAt ??= VietnamTime.Now;
            }

            return entity;
        }

        private static string BuildCoefficientName(int yearsOfService)
        {
            return yearsOfService switch
            {
                0 => "Under 5 years",
                5 => "5 to under 10 years",
                10 => "10 to under 15 years",
                15 => "15 to under 20 years",
                20 => "20 to under 25 years",
                25 => "25 years and above",
                _ => $"{yearsOfService} years"
            };
        }
    }
}
