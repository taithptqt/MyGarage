using AutoMapper;
using MyGarage.Application.Inventory;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class MaterialReturnService : GenericService<MaterialReturn>, IMaterialReturnService
    {
        private readonly IMaterialRequestDetailRepository _detailRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IStockTransactionRepository _transactionRepository;
        private readonly IServiceOrderDetailRepository _serviceOrderDetailRepository;
        private readonly IInventoryOperationsStore _store;
        private readonly IMapper _mapper;

        public MaterialReturnService(
            IMaterialReturnRepository repository,
            IMaterialRequestDetailRepository detailRepository,
            IInventoryRepository inventoryRepository,
            IStockTransactionRepository transactionRepository,
            IServiceOrderDetailRepository serviceOrderDetailRepository,
            IInventoryOperationsStore store,
            IMapper mapper) : base(repository)
        {
            _detailRepository = detailRepository;
            _inventoryRepository = inventoryRepository;
            _transactionRepository = transactionRepository;
            _serviceOrderDetailRepository = serviceOrderDetailRepository;
            _store = store;
            _mapper = mapper;
        }

        public async Task<MaterialReturn> ProcessReturnAsync(MaterialReturnCreateDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var returnedQuantity = dto.ReturnedQuantity ?? 0m;
            if (returnedQuantity <= 0)
                throw new InvalidOperationException("Số lượng trả phải lớn hơn 0.");

            return await _store.ExecuteInTransactionAsync(async ct =>
            {
                // 1. Lấy thông tin MaterialRequestDetail
                var detail = await _detailRepository.GetByIdAsync(dto.MaterialRequestDetailId);
                if (detail == null)
                    throw new KeyNotFoundException($"Không tìm thấy chi tiết yêu cầu vật tư ID: {dto.MaterialRequestDetailId}");

                // 2. Kiểm tra số lượng có thể trả
                decimal issued = detail.IssuedQuantity ?? 0;
                decimal alreadyReturned = detail.ReturnedQuantity ?? 0;
                decimal maxReturnable = issued - alreadyReturned;

                if (dto.ReturnedQuantity > maxReturnable)
                    throw new InvalidOperationException($"Số lượng trả ({dto.ReturnedQuantity}) vượt quá số lượng đã xuất có thể trả ({maxReturnable}).");

                // 3. Tạo bản ghi MaterialReturn
                var materialReturn = new MaterialReturn
                {
                    ReturnCode = string.IsNullOrEmpty(dto.ReturnCode)
                        ? $"RET-{VietnamTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}"
                        : dto.ReturnCode,
                    ServiceOrderId = detail.ServiceOrderDetail?.ServiceOrderId ?? dto.ServiceOrderId,
                    MaterialRequestDetailId = dto.MaterialRequestDetailId,
                    ProductId = detail.ProductId,
                    ReturnedQuantity = returnedQuantity,
                    ItemCondition = dto.ItemCondition ?? "GOOD",
                    ReturnedByStaffId = dto.ReturnedByStaffId,
                    ReceivedByStaffId = dto.ReceivedByStaffId,
                    ReturnTime = dto.ReturnTime ?? VietnamTime.Now,
                    Status = dto.Status ?? "COMPLETED",
                    Reason = dto.Reason,
                    Notes = dto.Notes,
                    CreatedAt = VietnamTime.Now,
                    CreatedBy = dto.CreatedBy
                };

                await _repository.AddAsync(materialReturn);

                // 4. Cập nhật tồn kho nếu tình trạng là GOOD
                if (materialReturn.ItemCondition.Equals("GOOD", StringComparison.OrdinalIgnoreCase))
                {
                    var inventory = await _store.GetTrackedInventoryAsync(detail.ProductId, detail.WarehouseId, ct);

                    if (inventory != null)
                    {
                        inventory.Quantity += materialReturn.ReturnedQuantity;
                    }
                    else
                    {
                        // Nếu chưa có dòng inventory (hiếm khi xảy ra vì đã xuất được), tạo mới
                        await _inventoryRepository.AddAsync(new Inventory
                        {
                            ProductId = detail.ProductId,
                            WarehouseId = detail.WarehouseId,
                            Quantity = materialReturn.ReturnedQuantity
                        });
                    }
                }

                // 5. Cập nhật MaterialRequestDetail
                detail.ReturnedQuantity = (detail.ReturnedQuantity ?? 0) + materialReturn.ReturnedQuantity;
                detail.UpdatedAt = VietnamTime.Now;
                detail.UpdatedBy = dto.CreatedBy;
                await _detailRepository.UpdateAsync(detail.Id, detail);

                // 6. Cập nhật ServiceOrderDetail (Giảm IssuedQuantity để giảm tiền tính bill)
                if (detail.ServiceOrderDetailId.HasValue)
                {
                    var sod = await _serviceOrderDetailRepository.GetByIdAsync(detail.ServiceOrderDetailId.Value);
                    if (sod != null)
                    {
                        sod.IssuedQuantity = (sod.IssuedQuantity ?? 0) - materialReturn.ReturnedQuantity;
                        if (sod.IssuedQuantity < 0) sod.IssuedQuantity = 0;

                        // Cập nhật SubTotal và FinalAmount nếu cần
                        sod.Quantity = sod.IssuedQuantity ?? 0m;
                        sod.SubTotal = sod.Quantity * sod.UnitPriceAtTime;
                        sod.TaxAmount = sod.SubTotal * (sod.TaxRate / 100m);
                        sod.FinalAmount = sod.SubTotal + sod.TaxAmount;
                        sod.UpdatedAt = VietnamTime.Now;
                        sod.UpdatedBy = dto.CreatedBy;

                        await _serviceOrderDetailRepository.UpdateAsync(sod.Id, sod);
                    }
                }

                // 7. Ghi nhận giao dịch kho
                await _transactionRepository.AddAsync(new StockTransaction
                {
                    WarehouseId = detail.WarehouseId,
                    ProductId = detail.ProductId,
                    Type = "IN",
                    Quantity = materialReturn.ReturnedQuantity,
                    MaterialReturnId = materialReturn.Id,
                    MaterialRequestId = detail.RequestId,
                    Note = $"Hoàn trả từ phiếu yêu cầu #{detail.RequestId}. Lý do: {materialReturn.Reason}",
                    CreatedAt = VietnamTime.Now,
                    CreatedBy = materialReturn.ReturnedByStaffId
                });

                return materialReturn;
            });
        }

        public List<MaterialReturnResponseDto> MapToDtos(IEnumerable<MaterialReturn> items)
        {
            return _mapper.Map<List<MaterialReturnResponseDto>>(items);
        }

        public async Task<MaterialReturnResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<MaterialReturnResponseDto>(item);
        }

        public async Task<MaterialReturnResponseDto> ProcessReturnDtoAsync(MaterialReturnCreateDto dto)
        {
            var createdItem = await ProcessReturnAsync(dto);
            return _mapper.Map<MaterialReturnResponseDto>(createdItem);
        }

        public async Task<MaterialReturnResponseDto?> UpdateDtoAsync(long id, MaterialReturnUpdateDto itemDto, MaterialReturn? existingItem = null)
        {
            existingItem ??= await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<MaterialReturnResponseDto>(updatedItem);
        }
    }
}
