using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class QcInspectionService : GenericService<QcInspection>, IQcInspectionService
    {
        private readonly IQcInspectionStore _store;
        private readonly IMapper _mapper;

        public QcInspectionService(
            IQcInspectionRepository repository,
            IQcInspectionStore store,
            IMapper mapper) : base(repository)
        {
            _store = store;
            _mapper = mapper;
        }

        public List<QcInspectionResponseDto> MapToDtos(IEnumerable<QcInspection> items)
        {
            return _mapper.Map<List<QcInspectionResponseDto>>(items);
        }

        public async Task<QcInspectionResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<QcInspectionResponseDto>(item);
        }

        public async Task<QcInspectionResponseDto> CreateDtoAsync(QcInspectionCreateDto itemDto)
        {
            var entity = _mapper.Map<QcInspection>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<QcInspectionResponseDto>(createdItem);
        }

        public async Task<QcInspectionResponseDto?> UpdateDtoAsync(long id, QcInspectionUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<QcInspectionResponseDto>(updatedItem);
        }

        public async Task<bool> PassAsync(long id, QcPassRequestDto request)
        {
            var inspection = await _store.GetInspectionForUpdateAsync(id);
            if (inspection == null) return false;

            await _store.ExecuteInTransactionAsync(async () =>
            {
                inspection.Status = nameof(QcInspectionStatusCode.PASS);
                inspection.InspectedAt = VietnamTime.Now;
                inspection.QcStaffId = request.InspectorId;
                inspection.OverallNote = request.Notes;
                await _repository.UpdateAsync(id, inspection);

                var order = await _store.GetServiceOrderForUpdateAsync(inspection.ServiceOrderId);
                if (order != null)
                {
                    order.Qcstatus = nameof(ServiceOrderQcStatusCode.PASS);
                    order.QcverifiedAt = inspection.InspectedAt;
                    order.QcinspectorId = request.InspectorId;

                    // Automatically move to READY for handover if QC passes
                    order.Status = nameof(ServiceOrderLifecycleStatus.READY);
                    order.UpdatedAt = VietnamTime.Now;
                }

                await _store.SaveChangesAsync();
            });

            return true;
        }

        public async Task<bool> FailAsync(long id, QcFailRequestDto request)
        {
            var inspection = await _store.GetInspectionForUpdateAsync(id);
            if (inspection == null) return false;

            await _store.ExecuteInTransactionAsync(async () =>
            {
                inspection.Status = nameof(QcInspectionStatusCode.FAIL);
                inspection.InspectedAt = VietnamTime.Now;
                inspection.QcStaffId = request.InspectorId;
                inspection.OverallNote = request.Reason;
                await _repository.UpdateAsync(id, inspection);

                var order = await _store.GetServiceOrderForUpdateAsync(inspection.ServiceOrderId);
                if (order != null)
                {
                    order.Qcstatus = nameof(ServiceOrderQcStatusCode.FAIL);
                    order.QcinspectorId = request.InspectorId;

                    // Revert to REPAIRING status on QC failure
                    order.Status = nameof(ServiceOrderLifecycleStatus.REPAIRING);
                    order.UpdatedAt = VietnamTime.Now;
                }

                await _store.SaveChangesAsync();
            });

            return true;
        }
    }
}
