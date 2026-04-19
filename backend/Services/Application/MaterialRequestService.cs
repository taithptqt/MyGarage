using MyGarage.Models;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace MyGarage.Services
{
    public class MaterialRequestService : GenericService<MaterialRequest>, IMaterialRequestService
    {
        private readonly IGarageContext _context;
        private readonly IWarehouseRepository _warehouseRepository;
        private readonly IMaterialRequestDetailRepository _detailRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IStockTransactionRepository _stockTransactionRepository;

        public MaterialRequestService(
            IMaterialRequestRepository repository,
            IGarageContext context,
            IWarehouseRepository warehouseRepository,
            IMaterialRequestDetailRepository detailRepository,
            IInventoryRepository inventoryRepository,
            IStockTransactionRepository stockTransactionRepository) : base(repository)
        {
            _context = context;
            _warehouseRepository = warehouseRepository;
            _detailRepository = detailRepository;
            _inventoryRepository = inventoryRepository;
            _stockTransactionRepository = stockTransactionRepository;
        }

        public async Task<MaterialRequest?> CreateFromQuotationAsync(long quotationId, long actorId)
        {
            // ... (Logic cũ vẫn giữ nguyên)
            var quotation = await _context.Quotations
                .Include(q => q.QuotationDetails)
                .Include(q => q.ServiceOrder)
                .FirstOrDefaultAsync(q => q.Id == quotationId);

            if (quotation == null) return null;

            var partItems = quotation.QuotationDetails
                .Where(d => d.ItemType == "PART" && d.ItemId.HasValue && d.Status != "CANCELED")
                .ToList();

            if (!partItems.Any()) return null;

            var warehouse = (await _warehouseRepository.GetAllAsync())
                .FirstOrDefault(w => w.BranchId == quotation.ServiceOrder.ExecuteBranchId && (w.IsActive ?? true));

            if (warehouse == null)
            {
                throw new InvalidOperationException($"Không tìm thấy kho hoạt động cho chi nhánh id: {quotation.ServiceOrder.ExecuteBranchId}");
            }

            var request = new MaterialRequest
            {
                ServiceOrderId = quotation.ServiceOrderId,
                RequestDate = VietnamTime.Now,
                Status = "PENDING",
                TechnicianId = actorId,
                Note = $"Tự động tạo từ báo giá {quotation.Code}",
                CreatedAt = VietnamTime.Now
            };

            await _repository.AddAsync(request);

            foreach (var item in partItems)
            {
                var productId = item.ItemId!.Value;
                var requestedQty = item.Quantity;
                string? statusNote = null;

                var inventory = await _inventoryRepository.GetByProductAndWarehouseAsync(productId, warehouse.Id);
                
                if (inventory != null)
                {
                    decimal currentReserved = inventory.ReservedQuantity ?? 0m;
                    decimal available = inventory.Quantity - currentReserved;

                    if (available < requestedQty)
                    {
                        statusNote = $"Thiếu hàng (Hiện có: {available:N0}, Yêu cầu: {requestedQty:N0})";
                    }

                    inventory.ReservedQuantity = currentReserved + requestedQty;
                    await _inventoryRepository.UpdateAsync(inventory.Id, inventory);
                }
                else
                {
                    statusNote = "Vật tư chưa có trong kho này";
                }

                var detail = new MaterialRequestDetail
                {
                    RequestId = request.Id,
                    ProductId = productId,
                    WarehouseId = warehouse.Id,
                    RequestedQuantity = requestedQty,
                    Status = "PENDING",
                    Note = statusNote,
                    CreatedAt = VietnamTime.Now,
                    CreatedBy = actorId
                };
                await _detailRepository.AddAsync(detail);
            }

            return request;
        }

        public async Task<MaterialRequest> IssuePartsAsync(MaterialRequestIssuanceDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var materialRequest = await _context.MaterialRequests
                    .FirstOrDefaultAsync(m => m.Id == request.RequestId);

                if (materialRequest == null)
                    throw new KeyNotFoundException($"Không tìm thấy phiếu yêu cầu materialRequestId: {request.RequestId}");

                if (materialRequest.Status == "ISSUED")
                    throw new InvalidOperationException("Phiếu yêu cầu này đã được xuất kho hoàn tất.");

                var materialRequestDetails = await _context.MaterialRequestDetails
                    .Where(d => d.RequestId == request.RequestId)
                    .ToListAsync();

                foreach (var item in request.Items)
                {
                    var detail = materialRequestDetails.FirstOrDefault(d => d.Id == item.DetailId);
                    if (detail == null) continue;

                    if (item.QuantityToIssue <= 0) continue;

                    // 1. Cập nhật tồn kho
                    var inventory = await _inventoryRepository.GetByProductAndWarehouseAsync(detail.ProductId, detail.WarehouseId);
                    if (inventory == null)
                        throw new InvalidOperationException($"Không tìm thấy tồn kho cho sản phẩm {detail.ProductId} tại kho {detail.WarehouseId}");

                    if (inventory.Quantity < item.QuantityToIssue)
                        throw new InvalidOperationException($"Tồn kho không đủ để xuất (Hiện có: {inventory.Quantity:N2}, Yêu cầu xuất: {item.QuantityToIssue:N2})");

                    inventory.Quantity -= item.QuantityToIssue;
                    // Giải phóng reservation (nếu có)
                    decimal reservationToRemove = Math.Min(inventory.ReservedQuantity ?? 0, item.QuantityToIssue);
                    inventory.ReservedQuantity -= reservationToRemove;

                    await _inventoryRepository.UpdateAsync(inventory.Id, inventory);

                    // 2. Ghi nhận giao dịch kho
                    var stockTx = new StockTransaction
                    {
                        ProductId = detail.ProductId,
                        WarehouseId = detail.WarehouseId,
                        Type = "OUT",
                        Quantity = item.QuantityToIssue,
                        MaterialRequestId = materialRequest.Id,
                        Note = request.Note ?? "Xuất kho vật tư sửa chữa",
                        CreatedAt = VietnamTime.Now,
                        CreatedBy = request.ActorStaffId
                    };
                    await _stockTransactionRepository.AddAsync(stockTx);

                    // 3. Cập nhật dòng chi tiết yêu cầu
                    detail.IssuedQuantity = (detail.IssuedQuantity ?? 0) + item.QuantityToIssue;
                    detail.Status = detail.IssuedQuantity >= detail.RequestedQuantity ? "ISSUED" : "PARTIALLY_ISSUED";
                    detail.UpdatedAt = VietnamTime.Now;
                    detail.UpdatedBy = request.ActorStaffId;
                    await _detailRepository.UpdateAsync(detail.Id, detail);
                }

                // 4. Cập nhật trạng thái tổng quát của phiếu
                bool allIssued = materialRequestDetails.Count > 0 && materialRequestDetails.All(d => d.Status == "ISSUED");
                materialRequest.Status = allIssued ? "ISSUED" : "PARTIALLY_ISSUED";
                await _repository.UpdateAsync(materialRequest.Id, materialRequest);

                await transaction.CommitAsync();
                return materialRequest;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}

