using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RetailReturnService : GenericService<RetailReturn>, IRetailReturnService
    {
        private readonly IMapper _mapper;

        public RetailReturnService(IRetailReturnRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<RetailReturnResponseDto> MapToDtos(IEnumerable<RetailReturn> items)
        {
            return _mapper.Map<List<RetailReturnResponseDto>>(items);
        }

        public RetailReturnResponseDto MapToDto(RetailReturn item)
        {
            return _mapper.Map<RetailReturnResponseDto>(item);
        }

        public async Task<RetailReturnResponseDto> CreatePendingDtoAsync(RetailReturnCreateDto itemDto, long actorId)
        {
            var entity = _mapper.Map<RetailReturn>(itemDto);
            entity.Status = nameof(RetailReturnStatusCode.PENDING);
            entity.CreatedBy = actorId;
            entity.UpdatedBy = null;
            entity.ApprovedBy = null;
            entity.ApprovedAt = null;
            entity.TotalRefundAmount = null;
            var createdItem = await AddAsync(entity);
            return _mapper.Map<RetailReturnResponseDto>(createdItem);
        }
    }
}
