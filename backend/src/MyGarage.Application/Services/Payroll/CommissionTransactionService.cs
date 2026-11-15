using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class CommissionTransactionService : GenericService<CommissionTransaction>, ICommissionTransactionService
    {
        private readonly IMapper _mapper;

        public CommissionTransactionService(ICommissionTransactionRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<CommissionTransactionResponseDto> MapToDtos(IEnumerable<CommissionTransaction> items)
            => _mapper.Map<List<CommissionTransactionResponseDto>>(items);

        public CommissionTransactionResponseDto MapToDto(CommissionTransaction item)
            => _mapper.Map<CommissionTransactionResponseDto>(item);

        public async Task<CommissionTransactionResponseDto> CreateDtoAsync(CommissionTransactionCreateDto itemDto)
        {
            var entity = _mapper.Map<CommissionTransaction>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<CommissionTransactionResponseDto>(createdItem);
        }

        public void MapUpdate(CommissionTransactionUpdateDto itemDto, CommissionTransaction existingItem)
            => _mapper.Map(itemDto, existingItem);
    }
}
