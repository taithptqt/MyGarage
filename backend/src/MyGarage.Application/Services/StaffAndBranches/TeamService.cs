using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class TeamService : GenericService<Team>, ITeamService
    {
        private readonly IMapper _mapper;

        public TeamService(ITeamRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public IEnumerable<TeamResponseDto> MapToDtos(IEnumerable<Team> items)
        {
            return _mapper.Map<IEnumerable<TeamResponseDto>>(items);
        }

        public async Task<TeamResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<TeamResponseDto>(item);
        }

        public async Task<TeamResponseDto> CreateDtoAsync(TeamCreateDto itemDto)
        {
            var entity = _mapper.Map<Team>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<TeamResponseDto>(createdItem);
        }

        public async Task<TeamResponseDto?> UpdateDtoAsync(long id, TeamUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<TeamResponseDto>(updatedItem);
        }
    }
}
