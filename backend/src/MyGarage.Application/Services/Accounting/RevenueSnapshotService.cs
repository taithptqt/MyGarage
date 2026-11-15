using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class RevenueSnapshotService : GenericService<RevenueSnapshot>, IRevenueSnapshotService
    {
        private readonly IMapper _mapper;

        public RevenueSnapshotService(IRevenueSnapshotRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<RevenueSnapshotResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<RevenueSnapshotResponseDto>>(items);
        }

        public async Task<RevenueSnapshotResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<RevenueSnapshotResponseDto>(item);
        }

        public async Task<RevenueSnapshotResponseDto> CreateDtoAsync(RevenueSnapshotCreateDto itemDto)
        {
            var entity = _mapper.Map<RevenueSnapshot>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<RevenueSnapshotResponseDto>(createdItem);
        }

        public async Task<RevenueSnapshotResponseDto?> UpdateDtoAsync(long id, RevenueSnapshotUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<RevenueSnapshotResponseDto>(updatedItem);
        }
    }
}
