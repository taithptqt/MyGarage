using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ServiceOrderService : GenericService<ServiceOrder>, IServiceOrderService
    {
        private readonly IGarageContext _context;
        private readonly IGroupSessionService _groupSessionService;
        private const int MaxCodeGenerationAttempts = 8;
        private const string CheckInLogType = "CHECK_IN";
        private const string CheckInPendingStatus = nameof(DiagnosisStatus.PENDING);
        private const string WorkshopDetailItemType = "LABOR";
        private const string WorkshopDetailDescription = "Dá»‹ch vá»¥ khÃ¡ch yÃªu cáº§u trÆ°á»›c";
        private const string WorkshopDetailUnit = "Láº§n";
        private const string WorkshopDetailPayerType = "CUSTOMER";
        private const string WorkshopDetailStatus = nameof(ServiceOrderDetailStatusCode.APPROVED);
        private const string WorkshopEditWindowPassedMessage = "Không thể sửa service order vì đã qua khung giờ đã chọn lúc tạo.";
        private static readonly HashSet<string> AllowedWorkshopOrderCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "QUICK_SERVICE",
            "GENERAL_REPAIR",
            "MAINTENANCE",
            "INSURANCE_REPAIR",
            "REWORK",
            "WARRANTLY",
            "WARRANTY",
        };

        public ServiceOrderService(
            IServiceOrderRepository repository,
            IGarageContext context,
            IGroupSessionService groupSessionService) : base(repository)
        {
            _context = context;
            _groupSessionService = groupSessionService;
        }

        public override async Task<ServiceOrder> AddAsync(ServiceOrder entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            Exception? lastDuplicateException = null;
            var hasExternalTransaction = _context.Database.CurrentTransaction != null;

            for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
            {
                var candidateCode = GenerateServiceOrderCode();

                var exists = await _context.ServiceOrders
                    .AsNoTracking()
                    .AnyAsync(x => x.Code == candidateCode);
                if (exists)
                {
                    continue;
                }

                entity.Code = candidateCode;

                IDbContextTransaction? attemptTransaction = null;
                if (!hasExternalTransaction)
                {
                    attemptTransaction = await _context.Database.BeginTransactionAsync();
                }

                try
                {
                    var createdOrder = await _repository.AddAsync(entity);
                    await EnsureCheckInRequestExistsAsync(createdOrder.Id);
                    if (attemptTransaction != null)
                    {
                        await attemptTransaction.CommitAsync();
                    }
                    return createdOrder;
                }
                catch (DbUpdateException ex) when (IsDuplicateCodeViolation(ex))
                {
                    lastDuplicateException = ex;
                    if (attemptTransaction != null)
                    {
                        await attemptTransaction.RollbackAsync();
                    }
                    DetachEntity(entity);
                }
                catch
                {
                    if (attemptTransaction != null)
                    {
                        await attemptTransaction.RollbackAsync();
                    }
                    throw;
                }
                finally
                {
                    if (attemptTransaction != null)
                    {
                        await attemptTransaction.DisposeAsync();
                    }
                }
            }

            throw new InvalidOperationException(
                "Khong the tao ma lenh sua chua duy nhat. Vui long thu lai.",
                lastDuplicateException);
        }

        public async Task<ServiceOrderBatchCreateResult> AddWorkshopBatchAsync(long? actorUserId, ServiceOrderBatchCreateRequestDto dto)
        {
            if (dto == null)
            {
                throw new InvalidOperationException("Dá»¯ liá»‡u táº¡o lá»‡nh sá»­a chá»¯a khÃ´ng há»£p lá»‡.");
            }

            var items = dto.Items?.Where(item => item != null).ToList() ?? new List<WorkshopServiceOrderCreateDto>();
            if (items.Count == 0)
            {
                throw new InvalidOperationException("Danh sÃ¡ch lá»‡nh sá»­a chá»¯a khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId);
                long? groupSessionId = null;

                if (dto.CreateGroupSession)
                {
                    var customerId = dto.CustomerId.GetValueOrDefault();
                    var branchId = dto.BranchId.GetValueOrDefault();
                    if (customerId <= 0 || branchId <= 0)
                    {
                        throw new InvalidOperationException("CustomerId vÃ  BranchId pháº£i lá»›n hÆ¡n 0 khi táº¡o nhÃ³m lá»‡nh.");
                    }

                    await EnsureCustomerExistsAsync(customerId);
                    var group = await _groupSessionService.CreateAsync(customerId, branchId, NormalizeText(dto.CustomerNote), actorStaffId);
                    groupSessionId = group.Id;
                }

                var createdOrders = new List<ServiceOrder>(items.Count);
                foreach (var item in items)
                {
                    var prepared = await PrepareWorkshopOrderEntityAsync(item, actorStaffId, groupSessionId);
                    var createdOrder = await AddAsync(prepared);
                    createdOrders.Add(createdOrder);
                }

                await transaction.CommitAsync();
                return new ServiceOrderBatchCreateResult
                {
                    GroupSessionId = groupSessionId,
                    Orders = createdOrders
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ServiceOrder?> UpdateWorkshopAsync(long id, WorkshopServiceOrderEditRequestDto dto, long? actorUserId = null)
        {
            if (id <= 0)
            {
                throw new InvalidOperationException("MÃ£ lá»‡nh sá»­a chá»¯a khÃ´ng há»£p lá»‡.");
            }

            if (dto == null)
            {
                throw new InvalidOperationException("Dá»¯ liá»‡u cáº­p nháº­t lá»‡nh sá»­a chá»¯a khÃ´ng há»£p lá»‡.");
            }

            var order = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
            if (order == null)
            {
                return null;
            }

            if (IsWorkshopEditWindowPassed(order, DateTime.Now))
            {
                throw new InvalidOperationException(WorkshopEditWindowPassedMessage);
            }

            var selectedServiceIds = dto.ServiceIds?
                .Select(NumberToPositiveLong)
                .Where(serviceId => serviceId > 0)
                .Distinct()
                .ToList() ?? new List<long>();
            if (selectedServiceIds.Count == 0)
            {
                throw new InvalidOperationException("Vui lÃ²ng chá»n Ã­t nháº¥t má»™t dá»‹ch vá»¥.");
            }

            var normalizedOrderCategory = NormalizeToken(dto.OrderCategory);
            if (!string.IsNullOrWhiteSpace(normalizedOrderCategory) &&
                !AllowedWorkshopOrderCategories.Contains(normalizedOrderCategory))
            {
                throw new InvalidOperationException($"PhÃ¢n loáº¡i lá»‡nh sá»­a chá»¯a khÃ´ng há»£p lá»‡: {dto.OrderCategory}.");
            }

            var serviceMap = await _context.StandardServices
                .AsNoTracking()
                .Where(s => selectedServiceIds.Contains(s.Id) && s.IsDeleted != true && s.IsActive != false)
                .ToDictionaryAsync(s => s.Id);
            if (serviceMap.Count != selectedServiceIds.Count)
            {
                throw new InvalidOperationException("CÃ³ dá»‹ch vá»¥ Ä‘Ã£ chá»n khÃ´ng cÃ²n há»£p lá»‡ hoáº·c Ä‘Ã£ bá»‹ xÃ³a.");
            }

            var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId);
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var checkInTime = dto.CheckInTime ?? order.CheckInTime ?? DateTime.Now;
                var estimatedHandover = dto.EstimatedHandover ?? order.EstimatedHandover;
                var promisedHandover = dto.PromisedHandoverTime ?? order.PromisedHandoverTime;

                if (estimatedHandover.HasValue && estimatedHandover.Value < checkInTime)
                {
                    throw new InvalidOperationException("Thá»i gian dá»± kiáº¿n hoÃ n thÃ nh pháº£i lá»›n hÆ¡n hoáº·c báº±ng thá»i Ä‘iá»ƒm xe vÃ o xÆ°á»Ÿng.");
                }

                if (promisedHandover.HasValue && promisedHandover.Value < checkInTime)
                {
                    throw new InvalidOperationException("Thá»i gian cam káº¿t tráº£ xe pháº£i lá»›n hÆ¡n hoáº·c báº±ng thá»i Ä‘iá»ƒm xe vÃ o xÆ°á»Ÿng.");
                }

                if (!string.IsNullOrWhiteSpace(normalizedOrderCategory))
                {
                    order.OrderCategory = normalizedOrderCategory;
                }
                order.ServiceAdvisorId = dto.ServiceAdvisorId;
                order.OldPartsReturnOption = NormalizeText(dto.OldPartsReturnOption) ?? order.OldPartsReturnOption;
                order.RequiredDepositAmount = Math.Max(dto.RequiredDepositAmount ?? 0m, 0m);
                order.CheckInTime = checkInTime;
                order.EstimatedHandover = estimatedHandover;
                order.PromisedHandoverTime = promisedHandover;
                order.Status = ResolveOrderStatus(dto.Status ?? order.Status);
                order.CustomerNotes = BuildSymptomsJson(dto.SymptomNotes, order.CustomerNotes);

                var now = DateTime.UtcNow;
                order.UpdatedAt = now;
                order.UpdatedBy = actorStaffId;

                var editableDetails = await _context.ServiceOrderDetails
                    .Where(d =>
                        d.ServiceOrderId == id &&
                        d.DiagnoseRecommendationId == null &&
                        (d.ItemType == "LABOR" || d.ItemType == "SERVICE"))
                    .OrderBy(d => d.Id)
                    .ToListAsync();

                var editableByServiceId = editableDetails
                    .Where(d => d.StandardServiceId.HasValue && d.StandardServiceId.Value > 0)
                    .GroupBy(d => d.StandardServiceId!.Value)
                    .ToDictionary(group => group.Key, group => group.ToList());

                var keptServiceIds = new HashSet<long>();
                foreach (var detail in editableDetails.Where(d => !d.StandardServiceId.HasValue || d.StandardServiceId.Value <= 0))
                {
                    _context.ServiceOrderDetails.Remove(detail);
                }

                foreach (var pair in editableByServiceId)
                {
                    var serviceId = pair.Key;
                    var rows = pair.Value;

                    if (!selectedServiceIds.Contains(serviceId))
                    {
                        foreach (var row in rows)
                        {
                            _context.ServiceOrderDetails.Remove(row);
                        }
                        continue;
                    }

                    var keep = rows[0];
                    ApplyWorkshopServiceDetailValues(keep, serviceMap[serviceId], actorStaffId, now);
                    if (rows.Count > 1)
                    {
                        foreach (var duplicate in rows.Skip(1))
                        {
                            _context.ServiceOrderDetails.Remove(duplicate);
                        }
                    }
                    keptServiceIds.Add(serviceId);
                }

                foreach (var serviceId in selectedServiceIds.Where(serviceId => !keptServiceIds.Contains(serviceId)))
                {
                    var service = serviceMap[serviceId];
                    var detail = new ServiceOrderDetail
                    {
                        ServiceOrderId = id,
                        StandardServiceId = service.Id,
                        CreatedAt = now,
                        CreatedBy = actorStaffId,
                        UpdatedAt = now,
                        UpdatedBy = actorStaffId
                    };
                    ApplyWorkshopServiceDetailValues(detail, service, actorStaffId, now);
                    _context.ServiceOrderDetails.Add(detail);
                }

                var subTotal = selectedServiceIds.Sum(serviceId => serviceMap[serviceId].BasePrice);
                var taxAmount = selectedServiceIds.Sum(serviceId =>
                    CalculateTaxAmount(serviceMap[serviceId].BasePrice, serviceMap[serviceId].TaxRate ?? 0m));
                order.SubTotalAmount = subTotal;
                order.TaxAmount = taxAmount;
                order.FinalAmount = subTotal + taxAmount - (order.TotalDiscount ?? 0m);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return order;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task EnsureCheckInRequestExistsAsync(long serviceOrderId)
        {
            if (serviceOrderId <= 0)
            {
                return;
            }

            var hasAnyCheckInLog = await _context.VehicleConditionLogs
                .AsNoTracking()
                .AnyAsync(x => x.ServiceOrderId == serviceOrderId && x.LogType == CheckInLogType);
            if (hasAnyCheckInLog)
            {
                return;
            }

            var now = DateTime.UtcNow;
            _context.VehicleConditionLogs.Add(new VehicleConditionLog
            {
                ServiceOrderId = serviceOrderId,
                LogType = CheckInLogType,
                DashboardWarningLights = CheckInPendingStatus,
                CreatedAt = now,
                UpdatedAt = now
            });

            await _context.SaveChangesAsync();
        }

        private async Task<ServiceOrder> PrepareWorkshopOrderEntityAsync(
            WorkshopServiceOrderCreateDto item,
            long? actorStaffId,
            long? forcedGroupSessionId)
        {
            if (item == null)
            {
                throw new InvalidOperationException("Dá»¯ liá»‡u lá»‡nh sá»­a chá»¯a khÃ´ng há»£p lá»‡.");
            }

            if (item.IsQuickOrder && item.IsNewVehicle)
            {
                throw new InvalidOperationException("KhÃ´ng thá»ƒ vá»«a lÃ  khÃ¡ch vÃ£ng lai vá»«a lÃ  xe má»›i cá»§a khÃ¡ch Ä‘Ã£ Ä‘Äƒng kÃ½.");
            }

            long customerId;
            long vehicleId;

            if (item.IsQuickOrder)
            {
                customerId = await ResolveOrCreateQuickCustomerAsync(item, actorStaffId);
                vehicleId = await ResolveOrCreateVehicleForOrderAsync(
                    item.GuestVehiclePlate,
                    item.GuestVehicleModelId,
                    item.GuestVehicleColor,
                    item.GuestVehicleVin,
                    item.GuestVehicleYear,
                    actorStaffId);
                await EnsureCustomerVehicleLinkForOrderAsync(customerId, vehicleId, actorStaffId);
            }
            else
            {
                customerId = item.CustomerId.GetValueOrDefault();
                if (customerId <= 0)
                {
                    throw new InvalidOperationException("Vui lÃ²ng chá»n khÃ¡ch hÃ ng há»£p lá»‡.");
                }
                await EnsureCustomerExistsAsync(customerId);

                if (item.IsNewVehicle)
                {
                    vehicleId = await ResolveOrCreateVehicleForOrderAsync(
                        item.VehicleLicensePlate,
                        item.VehicleModelId,
                        item.VehicleColor,
                        item.VehicleVin,
                        item.VehicleYear,
                        actorStaffId);
                    await EnsureCustomerVehicleLinkForOrderAsync(customerId, vehicleId, actorStaffId);
                }
                else
                {
                    vehicleId = item.VehicleId.GetValueOrDefault();
                    if (vehicleId <= 0)
                    {
                        throw new InvalidOperationException("Vui lÃ²ng chá»n xe há»£p lá»‡.");
                    }
                    await EnsureVehicleOwnershipAsync(customerId, vehicleId);
                }
            }

            var executeBranchId = item.ExecuteBranchId.GetValueOrDefault();
            var originBranchId = item.OriginBranchId.GetValueOrDefault();
            if (executeBranchId <= 0 && originBranchId <= 0)
            {
                throw new InvalidOperationException("Thiáº¿u thÃ´ng tin chi nhÃ¡nh thá»±c hiá»‡n lá»‡nh.");
            }
            if (executeBranchId <= 0) executeBranchId = originBranchId;
            if (originBranchId <= 0) originBranchId = executeBranchId;

            var now = DateTime.UtcNow;
            return new ServiceOrder
            {
                CustomerId = customerId,
                VehicleId = vehicleId,
                GroupSessionId = forcedGroupSessionId ?? item.GroupSessionId,
                OriginBranchId = originBranchId,
                ExecuteBranchId = executeBranchId,
                OrderCategory = NormalizeToken(item.OrderCategory),
                ServiceAdvisorId = item.ServiceAdvisorId,
                ReceptionistId = actorStaffId,
                CustomerNotes = NormalizeText(item.CustomerNotes),
                RequiredDepositAmount = item.RequiredDepositAmount,
                OldPartsReturnOption = NormalizeText(item.OldPartsReturnOption),
                CheckInTime = item.CheckInTime ?? DateTime.Now,
                EstimatedHandover = item.EstimatedHandover,
                PromisedHandoverTime = item.PromisedHandoverTime,
                Status = ResolveOrderStatus(item.Status),
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        private async Task<long> ResolveOrCreateQuickCustomerAsync(WorkshopServiceOrderCreateDto item, long? actorStaffId)
        {
            var guestName = NormalizeText(item.GuestName);
            if (string.IsNullOrWhiteSpace(guestName))
            {
                throw new InvalidOperationException("Vui lÃ²ng nháº­p há» tÃªn khÃ¡ch vÃ£ng lai.");
            }

            var normalizedPhone = NormalizePhone(item.GuestPhone);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                throw new InvalidOperationException("Vui lÃ²ng nháº­p sá»‘ Ä‘iá»‡n thoáº¡i khÃ¡ch vÃ£ng lai.");
            }

            var existing = await _context.Customers
                .FirstOrDefaultAsync(c =>
                    c.IsDeleted != true &&
                    c.Phone != null &&
                    c.Phone.Replace(" ", string.Empty) == normalizedPhone);

            if (existing != null)
            {
                var changed = false;
                if (!string.Equals(existing.FullName, guestName, StringComparison.Ordinal))
                {
                    existing.FullName = guestName;
                    changed = true;
                }

                if (!string.Equals(NormalizePhone(existing.Phone), normalizedPhone, StringComparison.Ordinal))
                {
                    existing.Phone = normalizedPhone;
                    changed = true;
                }

                if (existing.IsActive == false)
                {
                    existing.IsActive = true;
                    changed = true;
                }

                if (existing.IsDeleted == true)
                {
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    existing.DeletedBy = null;
                    changed = true;
                }

                if (changed)
                {
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = actorStaffId;
                    await _context.SaveChangesAsync();
                }

                return existing.Id;
            }

            var now = DateTime.UtcNow;
            var created = new Customer
            {
                FullName = guestName,
                Phone = normalizedPhone,
                Type = "GUEST",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId
            };

            _context.Customers.Add(created);
            await _context.SaveChangesAsync();
            return created.Id;
        }

        private async Task<long> ResolveOrCreateVehicleForOrderAsync(
            string? rawLicensePlate,
            long? rawModelId,
            string? rawColor,
            string? rawVin,
            int? rawYear,
            long? actorStaffId)
        {
            var licensePlate = NormalizeLicensePlate(rawLicensePlate);
            if (string.IsNullOrWhiteSpace(licensePlate))
            {
                throw new InvalidOperationException("Vui lÃ²ng nháº­p biá»ƒn sá»‘ xe há»£p lá»‡.");
            }

            var modelId = rawModelId.GetValueOrDefault();
            if (modelId <= 0)
            {
                throw new InvalidOperationException("Vui lÃ²ng chá»n dÃ²ng xe há»£p lá»‡.");
            }

            var existing = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.LicensePlate == licensePlate);
            if (existing != null)
            {
                var changed = false;
                var normalizedColor = NormalizeText(rawColor);
                var normalizedVin = NormalizeText(rawVin);

                if (string.IsNullOrWhiteSpace(existing.Color) && !string.IsNullOrWhiteSpace(normalizedColor))
                {
                    existing.Color = normalizedColor;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existing.Vin) && !string.IsNullOrWhiteSpace(normalizedVin))
                {
                    existing.Vin = normalizedVin;
                    changed = true;
                }

                if (!existing.ManufactureYear.HasValue && rawYear.HasValue && rawYear.Value > 0)
                {
                    existing.ManufactureYear = rawYear.Value;
                    changed = true;
                }

                if (existing.IsDeleted == true)
                {
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    existing.DeletedBy = null;
                    changed = true;
                }

                if (changed)
                {
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = actorStaffId;
                    await _context.SaveChangesAsync();
                }

                return existing.Id;
            }

            var now = DateTime.UtcNow;
            var created = new Vehicle
            {
                LicensePlate = licensePlate,
                ModelId = modelId,
                Color = NormalizeText(rawColor),
                Vin = NormalizeText(rawVin),
                ManufactureYear = rawYear.HasValue && rawYear.Value > 0 ? rawYear.Value : null,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId,
                IsDeleted = false
            };

            _context.Vehicles.Add(created);
            await _context.SaveChangesAsync();
            return created.Id;
        }

        private async Task EnsureCustomerVehicleLinkForOrderAsync(long customerId, long vehicleId, long? actorStaffId)
        {
            var existing = await _context.CustomerVehicles
                .FirstOrDefaultAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId);

            if (existing == null)
            {
                _context.CustomerVehicles.Add(new CustomerVehicle
                {
                    CustomerId = customerId,
                    VehicleId = vehicleId,
                    IsCurrentOwner = true,
                    OwnershipStartDate = DateOnly.FromDateTime(DateTime.Now),
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = actorStaffId,
                    UpdatedBy = actorStaffId
                });
                await _context.SaveChangesAsync();
                return;
            }

            var changed = false;
            if (existing.IsDeleted == true)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedBy = null;
                changed = true;
            }

            if (existing.IsCurrentOwner == false)
            {
                existing.IsCurrentOwner = true;
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = actorStaffId;
                await _context.SaveChangesAsync();
            }
        }

        private async Task EnsureCustomerExistsAsync(long customerId)
        {
            var exists = await _context.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == customerId && c.IsDeleted != true);
            if (!exists)
            {
                throw new InvalidOperationException("KhÃ¡ch hÃ ng Ä‘Ã£ chá»n khÃ´ng cÃ²n há»£p lá»‡.");
            }
        }

        private async Task EnsureVehicleOwnershipAsync(long customerId, long vehicleId)
        {
            var owned = await _context.CustomerVehicles
                .AsNoTracking()
                .AnyAsync(cv =>
                    cv.CustomerId == customerId &&
                    cv.VehicleId == vehicleId &&
                    cv.IsDeleted != true);
            if (!owned)
            {
                throw new InvalidOperationException("Xe Ä‘Ã£ chá»n khÃ´ng thuá»™c khÃ¡ch hÃ ng hiá»‡n táº¡i.");
            }
        }

        private void ApplyWorkshopServiceDetailValues(
            ServiceOrderDetail detail,
            StandardService service,
            long? actorStaffId,
            DateTime nowUtc)
        {
            var taxRate = service.TaxRate ?? 0m;
            var subTotal = service.BasePrice;
            var taxAmount = CalculateTaxAmount(subTotal, taxRate);

            detail.ItemType = WorkshopDetailItemType;
            detail.ItemName = service.Name;
            detail.Description = WorkshopDetailDescription;
            detail.Quantity = 1m;
            detail.Unit = WorkshopDetailUnit;
            detail.UnitPriceAtTime = service.BasePrice;
            detail.SubTotal = subTotal;
            detail.TaxRate = taxRate;
            detail.TaxAmount = taxAmount;
            detail.FinalAmount = subTotal + taxAmount;
            detail.PayerType = WorkshopDetailPayerType;
            detail.Status = WorkshopDetailStatus;
            detail.StandardServiceId = service.Id;
            detail.UpdatedAt = nowUtc;
            detail.UpdatedBy = actorStaffId;
        }

        private static decimal CalculateTaxAmount(decimal amount, decimal taxRate)
        {
            return Math.Round(amount * taxRate / 100m, 2, MidpointRounding.AwayFromZero);
        }

        private static string BuildSymptomsJson(IReadOnlyCollection<string>? symptomNotes, string? fallbackCustomerNotes)
        {
            if (symptomNotes == null)
            {
                return string.IsNullOrWhiteSpace(fallbackCustomerNotes)
                    ? JsonSerializer.Serialize(new { symptoms = new[] { "KhÃ´ng cÃ³ ghi chÃº gÃ¬" } })
                    : fallbackCustomerNotes;
            }

            var normalized = symptomNotes
                .Select(note => note?.Trim())
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .ToList();
            if (normalized.Count == 0)
            {
                normalized.Add("KhÃ´ng cÃ³ ghi chÃº gÃ¬");
            }

            return JsonSerializer.Serialize(new { symptoms = normalized });
        }

        private static long NumberToPositiveLong(long value)
        {
            return value > 0 ? value : 0;
        }


        private static bool IsWorkshopEditWindowPassed(ServiceOrder order, DateTime now)
        {
            var windowEnd = ResolveWorkshopEditWindowEnd(order);
            return windowEnd.HasValue && now >= windowEnd.Value;
        }

        private static DateTime? ResolveWorkshopEditWindowEnd(ServiceOrder order)
        {
            if (order.EstimatedHandover.HasValue)
            {
                return order.EstimatedHandover.Value;
            }

            if (order.PromisedHandoverTime.HasValue)
            {
                return order.PromisedHandoverTime.Value;
            }

            return null;
        }
        private static string GenerateServiceOrderCode()
        {
            var timestamp = DateTime.UtcNow.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture);
            var entropy = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            return $"SO-{timestamp}-{entropy}";
        }

        private static bool IsDuplicateCodeViolation(Exception ex)
        {
            var cursor = ex;
            while (cursor != null)
            {
                if (cursor is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                {
                    return true;
                }

                cursor = cursor.InnerException;
            }

            return false;
        }

        private static string NormalizePhone(string? value)
        {
            return string.Concat((value ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch))).Trim();
        }

        private static string? NormalizeText(string? value)
        {
            var text = value?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static string ResolveOrderStatus(string? status)
        {
            var token = NormalizeToken(status);
            if (string.IsNullOrWhiteSpace(token))
            {
                return nameof(ServiceOrderLifecycleStatus.NEW);
            }

            if (!Enum.TryParse<ServiceOrderLifecycleStatus>(token, ignoreCase: true, out _))
            {
                throw new InvalidOperationException($"Tráº¡ng thÃ¡i lá»‡nh sá»­a chá»¯a khÃ´ng há»£p lá»‡: {status}.");
            }

            return token;
        }

        private static string? NormalizeToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim()
                .ToUpperInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");
        }

        private static string NormalizeLicensePlate(string? rawLicensePlate)
        {
            return string.Concat((rawLicensePlate ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Where(ch => !char.IsWhiteSpace(ch)));
        }

        private void DetachEntity(ServiceOrder entity)
        {
            var entry = _context.Entry(entity);
            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
