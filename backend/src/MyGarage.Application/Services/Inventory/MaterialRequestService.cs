using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Inventory;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class MaterialRequestService : GenericService<MaterialRequest>, IMaterialRequestService
    {
        private readonly IInventoryOperationsStore _store;
        private readonly IWarehouseRepository _warehouseRepository;
        private readonly IMaterialRequestDetailRepository _detailRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IStockTransactionRepository _stockTransactionRepository;
        private readonly IMaterialRequestQueryService _queryService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IMapper _mapper;

        public MaterialRequestService(
            IMaterialRequestRepository repository,
            IInventoryOperationsStore store,
            IWarehouseRepository warehouseRepository,
            IMaterialRequestDetailRepository detailRepository,
            IInventoryRepository inventoryRepository,
            IStockTransactionRepository stockTransactionRepository,
            IMaterialRequestQueryService queryService,
            IServiceOrderService serviceOrderService,
            IMapper mapper) : base(repository)
        {
            _store = store;
            _warehouseRepository = warehouseRepository;
            _detailRepository = detailRepository;
            _inventoryRepository = inventoryRepository;
            _stockTransactionRepository = stockTransactionRepository;
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _serviceOrderService = serviceOrderService ?? throw new ArgumentNullException(nameof(serviceOrderService));
            _mapper = mapper;
        }

        public async Task<MaterialRequest?> CreateFromQuotationAsync(long quotationId, long actorId)
        {
            // ... (Logic cũ vẫn giữ nguyên)
            var quotation = await _store.GetQuotationWithDetailsAndServiceOrderAsync(quotationId);

            if (quotation == null) return null;

            var partItems = quotation.QuotationDetails
                .Where(d => string.Equals(d.ItemType?.Trim(), "PART", StringComparison.OrdinalIgnoreCase) &&
                    d.ItemId.HasValue && d.ItemId.Value > 0 && d.Quantity > 0 &&
                    !string.Equals(d.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var orderDetails = await _store.GetQuotationOrderDetailsAsync(quotationId);
            var detailsByQuotationId = orderDetails
                .Where(d => d.QuotationDetailId.HasValue)
                .GroupBy(d => d.QuotationDetailId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());
            if (partItems.Any(item => !detailsByQuotationId.TryGetValue(item.Id, out var rows) || rows.Count != 1))
                throw new InvalidOperationException("Không xác định được hạng mục lệnh sửa chữa tương ứng với phụ tùng báo giá.");

            var existingDetails = await _store.GetRequestsForOrderDetailsAsync(orderDetails.Select(d => d.Id).ToArray());
            var unlinkedDetails = await _store.GetUnlinkedRequestsForOrderAsync(quotation.ServiceOrderId);
            static bool IsActiveRequest(MaterialRequestDetail detail) =>
                !string.Equals(detail.Request.Status, "REJECTED", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(detail.Request.Status, "CANCELED", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(detail.Request.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);
            var canceledOrderIds = orderDetails.Where(d =>
                quotation.QuotationDetails.Any(q => q.Id == d.QuotationDetailId &&
                    string.Equals(q.Status, "CANCELED", StringComparison.OrdinalIgnoreCase)))
                .Select(d => d.Id).ToHashSet();
            if (existingDetails.Any(d => d.ServiceOrderDetailId.HasValue && canceledOrderIds.Contains(d.ServiceOrderDetailId.Value) && IsActiveRequest(d)))
                throw new InvalidOperationException("Hạng mục đã hủy còn yêu cầu kho; cần đối soát phiếu kho trước khi duyệt.");
            if (quotation.QuotationDetails.Any(q => q.SourceQuotationDetailId.HasValue && q.ItemId.HasValue &&
                string.Equals(q.ItemType?.Trim(), "PART", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(q.Status, "CANCELED", StringComparison.OrdinalIgnoreCase) &&
                unlinkedDetails.Any(d => d.ProductId == q.ItemId && IsActiveRequest(d))))
                throw new InvalidOperationException("Hạng mục đã hủy có phiếu kho lịch sử chưa liên kết; cần đối soát trước khi duyệt.");
            var quantitiesToRequest = new Dictionary<long, decimal>();
            foreach (var item in partItems)
            {
                if (item.SourceQuotationDetailId.HasValue && unlinkedDetails.Any(d => d.ProductId == item.ItemId && IsActiveRequest(d)))
                    throw new InvalidOperationException("Phiếu kho lịch sử chưa liên kết hạng mục; cần đối soát trước khi duyệt báo giá điều chỉnh.");
                var orderDetailId = detailsByQuotationId[item.Id][0].Id;
                var active = existingDetails.Where(d => d.ServiceOrderDetailId == orderDetailId && IsActiveRequest(d)).ToList();
                if (active.Any(d => d.ProductId != item.ItemId))
                    throw new InvalidOperationException("Phụ tùng đã yêu cầu kho thay đổi sản phẩm; cần đối soát phiếu kho trước khi duyệt.");
                var alreadyRequested = active.Sum(d => d.RequestedQuantity);
                if (alreadyRequested > item.Quantity)
                    throw new InvalidOperationException("Số lượng báo giá nhỏ hơn số lượng đã yêu cầu kho; cần đối soát phiếu kho trước khi duyệt.");
                quantitiesToRequest[item.Id] = item.Quantity - alreadyRequested;
            }
            partItems = partItems.Where(item => quantitiesToRequest[item.Id] > 0).ToList();
            if (partItems.Count == 0) return null;

            var warehouse = (await _warehouseRepository.GetAllAsync())
                .FirstOrDefault(w => w.BranchId == quotation.ServiceOrder.ExecuteBranchId && (w.IsActive ?? true));

            if (warehouse == null)
            {
                throw new InvalidOperationException($"Không tìm thấy kho hoạt động cho chi nhánh id: {quotation.ServiceOrder.ExecuteBranchId}");
            }

            return await _store.ExecuteInTransactionAsync(async _ =>
            {
                var request = new MaterialRequest
                {
                    ServiceOrderId = quotation.ServiceOrderId,
                    RequestDate = VietnamTime.Now,
                    Status = "PENDING",
                    TechnicianId = actorId > 0 ? actorId : null,
                    Note = $"Tự động tạo từ báo giá {quotation.Code}",
                    CreatedAt = VietnamTime.Now
                };

                await _repository.AddAsync(request);

                foreach (var item in partItems.OrderBy(x => x.ItemId).ThenBy(x => x.Id))
                {
                    var productId = item.ItemId!.Value;
                    var requestedQty = quantitiesToRequest[item.Id];
                    string? statusNote = null;

                    var inventory = await _store.GetTrackedInventoryAsync(productId, warehouse.Id);
                    var reservedForLine = 0m;

                    if (inventory != null)
                    {
                        decimal currentReserved = inventory.ReservedQuantity ?? 0m;
                        decimal available = Math.Max(0m, inventory.Quantity - currentReserved);

                        if (available < requestedQty)
                        {
                            statusNote = $"Thiếu hàng (Hiện có: {available:N0}, Yêu cầu: {requestedQty:N0})";
                        }

                        reservedForLine = Math.Min(available, requestedQty);
                        inventory.ReservedQuantity = currentReserved + reservedForLine;
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
                        ReservedQuantity = reservedForLine,
                        ServiceOrderDetailId = detailsByQuotationId[item.Id][0].Id,
                        Status = "PENDING",
                        Note = statusNote,
                        CreatedAt = VietnamTime.Now,
                        CreatedBy = actorId > 0 ? actorId : null
                    };
                    await _detailRepository.AddAsync(detail);
                }

                return request;
            });
        }

        public async Task<MaterialRequest> IssuePartsAsync(MaterialRequestIssuanceDto request)
        {
            return await _store.ExecuteInTransactionAsync(async ct =>
            {
                var materialRequest = await _store.GetMaterialRequestByIdAsync(request.RequestId, ct);

                if (materialRequest == null)
                    throw new KeyNotFoundException($"Không tìm thấy phiếu yêu cầu materialRequestId: {request.RequestId}");

                var requestStatus = (materialRequest.Status ?? string.Empty).Trim().ToUpperInvariant();
                if (requestStatus is not ("PENDING" or "PARTIALLY_ISSUED" or "PARTIAL" or "APPROVED"))
                    throw new InvalidOperationException("Phiếu yêu cầu không ở trạng thái cho phép xuất kho.");

                var materialRequestDetails = await _store.GetMaterialRequestDetailsAsync(request.RequestId, ct);

                if (request.Items == null || request.Items.Count == 0 || request.Items.Any(x => x == null) ||
                    request.Items.GroupBy(x => x.DetailId).Any(g => g.Count() > 1))
                    throw new InvalidOperationException("Danh sách vật tư xuất không hợp lệ hoặc có dòng trùng lặp.");

                foreach (var item in request.Items)
                {
                    var detail = materialRequestDetails.FirstOrDefault(d => d.Id == item.DetailId);
                    if (detail == null || item.QuantityToIssue <= 0 ||
                        item.QuantityToIssue > detail.RequestedQuantity - (detail.IssuedQuantity ?? 0m))
                        throw new InvalidOperationException("Số lượng xuất không hợp lệ hoặc vượt số lượng còn yêu cầu.");
                }

                foreach (var item in request.Items.OrderBy(x => materialRequestDetails.First(d => d.Id == x.DetailId).WarehouseId)
                    .ThenBy(x => materialRequestDetails.First(d => d.Id == x.DetailId).ProductId).ThenBy(x => x.DetailId))
                {
                    var detail = materialRequestDetails.FirstOrDefault(d => d.Id == item.DetailId);
                    if (detail == null) throw new InvalidOperationException("Không tìm thấy vật tư trong phiếu yêu cầu.");

                    // 1. Cập nhật tồn kho
                    var inventory = await _store.GetTrackedInventoryAsync(detail.ProductId, detail.WarehouseId, ct);
                    if (inventory == null)
                        throw new InvalidOperationException($"Không tìm thấy tồn kho cho sản phẩm {detail.ProductId} tại kho {detail.WarehouseId}");

                    var reservedForLine = ResolveLineReservation(detail, inventory.ReservedQuantity ?? 0m);
                    var totalReserved = inventory.ReservedQuantity ?? 0m;
                    if (reservedForLine > totalReserved)
                        throw new InvalidOperationException("Số giữ chỗ của phiếu không khớp tồn kho; cần đối soát.");
                    var availableToIssue = Math.Max(0m, inventory.Quantity - totalReserved + reservedForLine);
                    if (availableToIssue < item.QuantityToIssue)
                        throw new InvalidOperationException($"Tồn kho khả dụng không đủ để xuất (Hiện có: {availableToIssue:N2}, Yêu cầu xuất: {item.QuantityToIssue:N2})");

                    inventory.Quantity -= item.QuantityToIssue;
                    // Giải phóng reservation (nếu có)
                    decimal reservationToRemove = Math.Min(reservedForLine, item.QuantityToIssue);
                    inventory.ReservedQuantity = totalReserved - reservationToRemove;
                    detail.ReservedQuantity = reservedForLine - reservationToRemove;

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
                if (request.ActorStaffId > 0)
                {
                    materialRequest.StorekeeperId = request.ActorStaffId;
                }
                await _repository.UpdateAsync(materialRequest.Id, materialRequest);

                return materialRequest;
            });
        }

        public async Task<MaterialRequest> RejectRequestAsync(long requestId, long actorStaffId, string? reason)
        {
            return await _store.ExecuteInTransactionAsync(async ct =>
            {
                var materialRequest = await _store.GetMaterialRequestByIdAsync(requestId, ct);

                if (materialRequest == null)
                    throw new KeyNotFoundException($"Không tìm thấy phiếu yêu cầu materialRequestId: {requestId}");

                var currentStatus = (materialRequest.Status ?? string.Empty).Trim().ToUpperInvariant();
                if (currentStatus is "ISSUED" or "PARTIALLY_ISSUED")
                    throw new InvalidOperationException("Phiếu yêu cầu đã xuất kho, không thể từ chối.");

                if (currentStatus == "REJECTED")
                    throw new InvalidOperationException("Phiếu yêu cầu này đã bị từ chối trước đó.");

                var details = await _store.GetMaterialRequestDetailsAsync(requestId, ct);

                foreach (var detail in details.OrderBy(d => d.WarehouseId).ThenBy(d => d.ProductId).ThenBy(d => d.Id))
                {
                    var remaining = Math.Max(0m, detail.RequestedQuantity - (detail.IssuedQuantity ?? 0m));
                    if (remaining > 0m)
                    {
                        var inventory = await _store.GetTrackedInventoryAsync(detail.ProductId, detail.WarehouseId, ct);
                        var reservedForLine = ResolveLineReservation(detail, inventory?.ReservedQuantity ?? 0m);
                        if (reservedForLine > (inventory?.ReservedQuantity ?? 0m))
                            throw new InvalidOperationException("Số giữ chỗ của phiếu không khớp tồn kho; cần đối soát.");
                        if (inventory != null && reservedForLine > 0m)
                        {
                            decimal currentReserved = inventory.ReservedQuantity ?? 0m;
                            inventory.ReservedQuantity = currentReserved - reservedForLine;
                            await _inventoryRepository.UpdateAsync(inventory.Id, inventory);
                        }
                        detail.ReservedQuantity = 0m;
                    }

                    detail.Status = "REJECTED";
                    detail.UpdatedAt = VietnamTime.Now;
                    detail.UpdatedBy = actorStaffId > 0 ? actorStaffId : null;
                    await _detailRepository.UpdateAsync(detail.Id, detail);
                }

                materialRequest.Status = "REJECTED";
                if (actorStaffId > 0)
                {
                    materialRequest.StorekeeperId = actorStaffId;
                }

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    materialRequest.Note = string.IsNullOrWhiteSpace(materialRequest.Note)
                        ? $"Từ chối: {reason.Trim()}"
                        : $"{materialRequest.Note} | Từ chối: {reason.Trim()}";
                }

                await _repository.UpdateAsync(materialRequest.Id, materialRequest);

                return materialRequest;
            });
        }

        public async Task<MaterialRequestResponseDto> CreateDtoAsync(MaterialRequestCreateDto itemDto, long? defaultTechnicianId = null)
        {
            var entity = _mapper.Map<MaterialRequest>(itemDto);
            entity.TechnicianId ??= defaultTechnicianId;
            var createdItem = await AddAsync(entity);
            return _mapper.Map<MaterialRequestResponseDto>(createdItem);
        }

        public async Task<MaterialRequestResponseDto?> UpdateDtoAsync(long id, MaterialRequestUpdateDto itemDto, MaterialRequest? existingItem = null)
        {
            existingItem ??= await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<MaterialRequestResponseDto>(updatedItem);
        }

        public async Task<PagedEnvelope<MaterialRequestSummaryDto>> GetSummaryPageAsync(
            long? scopedBranchId,
            string? status,
            int? page,
            int? pageSize,
            CancellationToken ct = default)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (pagedRequests, total) = await _queryService.GetPageAsync(
                scopedBranchId,
                status,
                normalizedPage,
                normalizedSize,
                ct);
            var stockLookup = await _queryService.LoadInventoryLookupAsync(pagedRequests, ct);
            var payload = pagedRequests.Select(mr => MapSummaryDto(mr, stockLookup)).ToList();
            return PagedEnvelope<MaterialRequestSummaryDto>.Create(payload, total, normalizedPage, normalizedSize);
        }

        public async Task<MaterialRequestCommandResult<MaterialRequestSummaryDto>> GetScopedSummaryByIdAsync(
            long id,
            long? scopedBranchId,
            CancellationToken ct = default)
        {
            var mr = await _queryService.GetByIdDetailedAsync(id, ct);
            if (mr == null)
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.NotFound();
            }

            if (!await _queryService.BelongsToBranchScopeAsync(mr.Id, scopedBranchId, ct))
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.Forbidden();
            }

            var stockLookup = await _queryService.LoadInventoryLookupAsync(new[] { mr }, ct);
            return MaterialRequestCommandResult<MaterialRequestSummaryDto>.Ok(MapSummaryDto(mr, stockLookup));
        }

        public async Task<MaterialRequestCommandResult<MaterialRequestResponseDto>> CreateScopedAsync(
            MaterialRequestCreateDto? itemDto,
            long? scopedBranchId,
            long? actorUserId,
            CancellationToken ct = default)
        {
            if (itemDto == null)
            {
                return MaterialRequestCommandResult<MaterialRequestResponseDto>.BadRequest("Dữ liệu phiếu yêu cầu không hợp lệ.");
            }

            try
            {
                if (itemDto.ServiceOrderId > 0)
                {
                    if (scopedBranchId.HasValue)
                    {
                        var orderBranchId = await _queryService.GetServiceOrderExecuteBranchIdAsync(itemDto.ServiceOrderId.Value, ct);
                        if (orderBranchId.HasValue && orderBranchId.Value != scopedBranchId.Value)
                        {
                            return MaterialRequestCommandResult<MaterialRequestResponseDto>.Forbidden();
                        }
                    }

                    await _serviceOrderService.EnsurePartsEditableAsync(itemDto.ServiceOrderId.Value);
                }

                var responseDto = await CreateDtoAsync(itemDto, actorUserId);
                return MaterialRequestCommandResult<MaterialRequestResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return MaterialRequestCommandResult<MaterialRequestResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<MaterialRequestCommandResult<MaterialRequestResponseDto>> UpdateScopedAsync(
            long id,
            MaterialRequestUpdateDto? itemDto,
            long? scopedBranchId,
            CancellationToken ct = default)
        {
            if (itemDto == null)
            {
                return MaterialRequestCommandResult<MaterialRequestResponseDto>.BadRequest("Dữ liệu cập nhật phiếu yêu cầu không hợp lệ.");
            }

            try
            {
                var existingItem = await GetByIdAsync(id);
                if (existingItem == null)
                {
                    return MaterialRequestCommandResult<MaterialRequestResponseDto>.NotFound();
                }

                if (!await _queryService.BelongsToBranchScopeAsync(id, scopedBranchId, ct))
                {
                    return MaterialRequestCommandResult<MaterialRequestResponseDto>.Forbidden();
                }

                var targetServiceOrderId = itemDto.ServiceOrderId ?? existingItem.ServiceOrderId;
                if (targetServiceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(targetServiceOrderId.Value);
                }

                var responseDto = await UpdateDtoAsync(id, itemDto, existingItem);
                if (responseDto == null)
                {
                    return MaterialRequestCommandResult<MaterialRequestResponseDto>.NotFound();
                }

                return MaterialRequestCommandResult<MaterialRequestResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return MaterialRequestCommandResult<MaterialRequestResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<MaterialRequestCommandResult<MaterialRequestSummaryDto>> IssueScopedAsync(
            long id,
            long? scopedBranchId,
            long? currentUserId,
            long? fallbackActorStaffId,
            string? note,
            IReadOnlyList<MaterialRequestIssuanceItemDto>? requestedItems,
            CancellationToken ct = default)
        {
            var mr = await _queryService.GetWithDetailsByIdAsync(id, ct);
            if (mr == null)
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.NotFound(
                    $"Không tìm thấy phiếu yêu cầu materialRequestId: {id}");
            }

            if (!await _queryService.BelongsToBranchScopeAsync(id, scopedBranchId, ct))
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.Forbidden();
            }

            var actorId = currentUserId ?? fallbackActorStaffId ?? 0;
            if (actorId <= 0)
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.BadRequest(
                    "Không xác định được nhân viên thực hiện xuất kho.");
            }

            var items = ResolveIssuanceItems(mr.MaterialRequestDetails, requestedItems);
            if (items.Count == 0)
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.BadRequest(
                    "Phiếu yêu cầu không còn vật tư cần xuất.");
            }

            try
            {
                var updated = await IssuePartsAsync(new MaterialRequestIssuanceDto
                {
                    RequestId = id,
                    ActorStaffId = actorId,
                    Note = note,
                    Items = items
                });

                return await GetScopedSummaryByIdAsync(updated.Id, scopedBranchId, ct);
            }
            catch (KeyNotFoundException ex)
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.BadRequest(ex.Message);
            }
        }

        public async Task<MaterialRequestCommandResult<MaterialRequestSummaryDto>> RejectScopedAsync(
            long id,
            long? scopedBranchId,
            long? currentUserId,
            long? fallbackActorStaffId,
            string? reason,
            CancellationToken ct = default)
        {
            var exists = await _queryService.ExistsAsync(id, ct);
            if (!exists)
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.NotFound(
                    $"Không tìm thấy phiếu yêu cầu materialRequestId: {id}");
            }

            if (!await _queryService.BelongsToBranchScopeAsync(id, scopedBranchId, ct))
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.Forbidden();
            }

            var actorId = currentUserId ?? fallbackActorStaffId ?? 0;

            try
            {
                var updated = await RejectRequestAsync(id, actorId, reason);
                return await GetScopedSummaryByIdAsync(updated.Id, scopedBranchId, ct);
            }
            catch (KeyNotFoundException ex)
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return MaterialRequestCommandResult<MaterialRequestSummaryDto>.BadRequest(ex.Message);
            }
        }

        public async Task<MaterialRequestCommandResult<bool>> DeleteScopedAsync(
            long id,
            long? scopedBranchId,
            CancellationToken ct = default)
        {
            try
            {
                var existingItem = await GetByIdAsync(id);
                if (existingItem == null)
                {
                    return MaterialRequestCommandResult<bool>.NotFound();
                }

                if (!await _queryService.BelongsToBranchScopeAsync(id, scopedBranchId, ct))
                {
                    return MaterialRequestCommandResult<bool>.Forbidden();
                }

                if (existingItem.ServiceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(existingItem.ServiceOrderId.Value);
                }

                var result = await DeleteAsync(id);
                if (!result)
                {
                    return MaterialRequestCommandResult<bool>.NotFound();
                }

                return MaterialRequestCommandResult<bool>.Ok(true);
            }
            catch (InvalidOperationException ex)
            {
                return MaterialRequestCommandResult<bool>.BadRequest(ex.Message);
            }
        }

        internal static List<MaterialRequestIssuanceItemDto> ResolveIssuanceItems(
            IEnumerable<MaterialRequestDetail>? details,
            IReadOnlyList<MaterialRequestIssuanceItemDto>? requestedItems)
        {
            if (requestedItems != null)
            {
                return requestedItems.ToList();
            }

            return (details ?? Enumerable.Empty<MaterialRequestDetail>())
                    .Select(d => new MaterialRequestIssuanceItemDto
                    {
                        DetailId = d.Id,
                        QuantityToIssue = Math.Max(0m, d.RequestedQuantity - (d.IssuedQuantity ?? 0m))
                    })
                    .Where(x => x.QuantityToIssue > 0m)
                    .ToList();
        }

        internal static decimal ResolveLineReservation(MaterialRequestDetail detail, decimal totalReserved)
        {
            if (!detail.ReservedQuantity.HasValue && totalReserved > 0m)
                throw new InvalidOperationException("Phiếu lịch sử chưa xác định số giữ chỗ; cần đối soát trước khi xuất hoặc từ chối.");
            var reserved = detail.ReservedQuantity ?? 0m;
            if (reserved < 0m || reserved > Math.Max(0m, detail.RequestedQuantity - (detail.IssuedQuantity ?? 0m)))
                throw new InvalidOperationException("Số giữ chỗ của phiếu không hợp lệ; cần đối soát.");
            return reserved;
        }

        internal static decimal ResolveUnitCost(MyGarage.Models.Inventory? inventory, Product? product)
            => inventory?.CurrentMacPrice ?? product?.CostPrice ?? 0m;

        internal static MaterialRequestSummaryDto MapSummaryDto(
            MaterialRequest mr,
            IReadOnlyDictionary<(long ProductId, long WarehouseId), MyGarage.Models.Inventory> stockLookup)
        {
            return new MaterialRequestSummaryDto
            {
                Id = mr.Id,
                ServiceOrderId = mr.ServiceOrderId,
                ServiceOrderCode = mr.ServiceOrder?.Code,
                TechnicianId = mr.TechnicianId,
                RequesterName = mr.Technician?.FullName,
                StorekeeperId = mr.StorekeeperId,
                StorekeeperName = mr.Storekeeper?.FullName,
                RequestDate = mr.RequestDate,
                CreatedAt = mr.CreatedAt ?? mr.RequestDate,
                Status = mr.Status,
                IsRework = mr.IsRework,
                Note = mr.Note,
                Notes = mr.Note,
                Items = (mr.MaterialRequestDetails ?? Enumerable.Empty<MaterialRequestDetail>()).Select(d =>
                {
                    stockLookup.TryGetValue((d.ProductId, d.WarehouseId), out var inv);
                    return new MaterialRequestSummaryItemDto
                    {
                        Id = d.Id,
                        ProductId = d.ProductId,
                        WarehouseId = d.WarehouseId,
                        ProductName = d.Product?.Name,
                        ProductCode = d.Product?.Sku,
                        Sku = d.Product?.Sku,
                        Unit = d.Product?.Unit,
                        Quantity = d.RequestedQuantity,
                        RequestedQuantity = d.RequestedQuantity,
                        IssuedQuantity = d.IssuedQuantity ?? 0m,
                        Status = d.Status,
                        CurrentStock = inv?.Quantity ?? 0m,
                        UnitCost = ResolveUnitCost(inv, d.Product)
                    };
                }).ToList()
            };
        }
    }
}
