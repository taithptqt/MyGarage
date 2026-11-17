using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AuditLogService : GenericService<AuditLog>, IAuditLogService, IAuditLogReader, IAuditLogWriter
    {
        private readonly IAuditLogStore _store;
        private readonly IMapper _mapper;

        public AuditLogService(IAuditLogRepository repository, IAuditLogStore store, IMapper mapper) : base(repository)
        {
            _store = store;
            _mapper = mapper;
        }

        public Task<(IReadOnlyList<AuditLog> Items, int Total)> GetAdminChangesAsync(
            int page,
            int pageSize,
            string? action = null,
            string? tableName = null,
            string? query = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 1 : (pageSize > 200 ? 200 : pageSize);

            return _store.GetAdminChangesAsync(page, pageSize, action, tableName, query, from, to);
        }

        public List<AuditLogResponseDto> MapToDtos(IEnumerable<AuditLog> items)
        {
            return _mapper.Map<List<AuditLogResponseDto>>(items);
        }

        public async Task<AuditLogResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<AuditLogResponseDto>(item);
        }

        public async Task<AuditLogResponseDto> CreateDtoAsync(AuditLogCreateDto itemDto)
        {
            var entity = _mapper.Map<AuditLog>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<AuditLogResponseDto>(createdItem);
        }

        public async Task<AuditLogResponseDto?> UpdateDtoAsync(long id, AuditLogUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<AuditLogResponseDto>(updatedItem);
        }
    }
}
