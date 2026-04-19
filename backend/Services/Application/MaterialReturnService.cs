using MyGarage.Models;
using MyGarage.Interfaces;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.Common;

namespace MyGarage.Services
{
    public class MaterialReturnService : GenericService<MaterialReturn>, IMaterialReturnService
    {
        private readonly IMaterialRequestDetailRepository _detailRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IStockTransactionRepository _transactionRepository;
        private readonly IServiceOrderDetailRepository _serviceOrderDetailRepository;
        private readonly GarageContext _context;

        public MaterialReturnService(
            IMaterialReturnRepository repository,
            IMaterialRequestDetailRepository detailRepository,
            IInventoryRepository inventoryRepository,
            IStockTransactionRepository transactionRepository,
            IServiceOrderDetailRepository serviceOrderDetailRepository,
            GarageContext context) : base(repository)
        {
            _detailRepository = detailRepository;
            _inventoryRepository = inventoryRepository;
            _transactionRepository = transactionRepository;
            _serviceOrderDetailRepository = serviceOrderDetailRepository;
            _context = context;
        }

        public async Task<MaterialReturn> ProcessReturnAsync(MaterialReturnCreateDto dto)
        {
            if (dto.ReturnedQuantity <= 0)
                throw new InvalidOperationException("Số lượng trả phải lớn hơn 0.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
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
                    ReturnedQuantity = dto.ReturnedQuantity.Value,
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
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == detail.ProductId && i.WarehouseId == detail.WarehouseId);

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
                        sod.Quantity = sod.IssuedQuantity.Value; 
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

                await transaction.CommitAsync();
                return materialReturn;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
