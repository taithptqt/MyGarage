using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class ServiceOrderService : GenericService<ServiceOrder>, IServiceOrderService
    {
        private readonly IGarageContext _context;
        private readonly IGroupSessionService _groupSessionService;
        private readonly ISystemClock _clock;
        private const int MaxCodeGenerationAttempts = 8;
        private const string CheckInLogType = "CHECK_IN";
        private const string CheckInPendingStatus = nameof(DiagnosisStatus.PENDING);
        private const string WorkshopDetailItemType = "LABOR";
        private const string WorkshopDetailDescription = "Dịch vụ khách yêu cầu trước";
        private const string WorkshopDetailUnit = "Lần";
        private const string WorkshopDetailStatus = nameof(ServiceOrderDetailStatusCode.APPROVED);
        private const string WorkshopEditWindowPassedMessage = "Không thể sửa service order vì đã qua mốc vận hành cho phép chỉnh sửa.";
        private static readonly HashSet<string> WorkshopMilestoneLockedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ServiceOrderLifecycleStatus.READY),
            nameof(ServiceOrderLifecycleStatus.WAITING_PAYMENT),
            nameof(ServiceOrderLifecycleStatus.CLOSED),
            nameof(ServiceOrderLifecycleStatus.CANCELED),
        };
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
            IGroupSessionService groupSessionService,
            ISystemClock clock) : base(repository)
        {
            _context = context;
            _groupSessionService = groupSessionService;
            _clock = clock;
        }

        public override async Task<ServiceOrder> AddAsync(ServiceOrder entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
            var checkInTime = entity.CheckInTime ?? VietnamTime.Now;
            var executeBranchId = entity.ExecuteBranchId > 0 ? entity.ExecuteBranchId : entity.OriginBranchId;
            await EnsureBranchOpenForCheckInAsync(executeBranchId, checkInTime);

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

        public override async Task<ServiceOrder?> UpdateAsync(object id, ServiceOrder entity)
        {
            if (!TryParsePositiveServiceOrderId(id, out var serviceOrderId))
            {
                throw new InvalidOperationException("Mã lệnh sửa chữa không hợp lệ.");
            }

            var checkInTime = entity?.CheckInTime ?? VietnamTime.Now;
            var executeBranchId = entity?.ExecuteBranchId > 0
                ? entity.ExecuteBranchId
                : (entity?.OriginBranchId ?? 0);
            await EnsureBranchOpenForCheckInAsync(executeBranchId, checkInTime);
            await EnsureWorkshopEditableAsync(serviceOrderId);
            return await base.UpdateAsync(id, entity);
        }

        public override async Task<bool> DeleteAsync(object id)
        {
            if (!TryParsePositiveServiceOrderId(id, out var serviceOrderId))
            {
                throw new InvalidOperationException("Mã lệnh sửa chữa không hợp lệ.");
            }

            await EnsureWorkshopEditableAsync(serviceOrderId);
            return await base.DeleteAsync(id);
        }

        public async Task<ServiceOrderBatchCreateResult> AddWorkshopBatchAsync(long? actorUserId, ServiceOrderBatchCreateRequestDto dto)
        {
            if (dto == null)
            {
                throw new InvalidOperationException("Dữ liệu tạo lệnh sửa chữa không hợp lệ.");
            }

            var items = dto.Items?.Where(item => item != null).ToList() ?? new List<WorkshopServiceOrderCreateDto>();
            if (items.Count == 0)
            {
                throw new InvalidOperationException("Danh sách lệnh sửa chữa không được để trống.");
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
                        throw new InvalidOperationException("CustomerId và BranchId phải lớn hơn 0 khi tạo nhóm lệnh.");
                    }

                    await EnsureCustomerExistsAsync(customerId);
                    var group = await _groupSessionService.CreateAsync(customerId, branchId, NormalizeText(dto.CustomerNote), actorStaffId);
                    groupSessionId = group.Id;
                }

                var createdOrders = new List<ServiceOrder>(items.Count);
                foreach (var item in items)
                {
                    var selection = await BuildWorkshopServiceSelectionAsync(item.ServiceIds, item.PackageIds);
                    var prepared = await PrepareWorkshopOrderEntityAsync(item, actorStaffId, groupSessionId);
                    await EnsureWorkshopSelectionCompatibleWithVehicleAsync(selection, prepared.VehicleId);
                    prepared.RequestedPackageIds = selection.RequestedPackageIds;
                    var createdOrder = await AddAsync(prepared);
                    await AddWorkshopServiceDetailsAsync(createdOrder, selection, actorStaffId, item.InsuranceCompanyId);
                    await PersistServiceOrderInsuranceAssetsAsync(
                        createdOrder.Id,
                        item.InsuranceCardImageUrls,
                        item.InsuranceDamageImageUrls);
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
                throw new InvalidOperationException("Mã lệnh sửa chữa không hợp lệ.");
            }

            if (dto == null)
            {
                throw new InvalidOperationException("Dữ liệu cập nhật lệnh sửa chữa không hợp lệ.");
            }

            var order = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == id);
            if (order == null)
            {
                return null;
            }

            await EnsureWorkshopEditableAsync(id);

            var selection = await BuildWorkshopServiceSelectionAsync(dto.ServiceIds, dto.PackageIds);
            var selectedServiceIds = selection.EffectiveServiceIds;
            if (!selection.HasSelection)
            {
                throw new InvalidOperationException("Vui lòng chọn ít nhất một dịch vụ.");
            }

            await EnsureWorkshopSelectionCompatibleWithVehicleAsync(selection, order.VehicleId);

            var normalizedOrderCategory = NormalizeToken(dto.OrderCategory);
            if (!string.IsNullOrWhiteSpace(normalizedOrderCategory) &&
                !AllowedWorkshopOrderCategories.Contains(normalizedOrderCategory))
            {
                throw new InvalidOperationException($"Phân loại lệnh sửa chữa không hợp lệ: {dto.OrderCategory}.");
            }

            var serviceMap = selection.ServiceMap;

            var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId);
            var insuranceCompanyId = await ResolveOrderInsuranceCompanyIdAsync(order, dto.InsuranceCompanyId);
            var currentOrderCategory = !string.IsNullOrWhiteSpace(normalizedOrderCategory)
                ? normalizedOrderCategory
                : NormalizeToken(order.OrderCategory);
            var hasInsuranceSignalService = selectedServiceIds.Any(serviceId => InsuranceBillingRules.IsInsuranceSignalService(serviceMap[serviceId]));
            var insuranceOrderContext = insuranceCompanyId.HasValue &&
                (string.Equals(currentOrderCategory, InsuranceBillingRules.InsuranceRepairCategoryCode, StringComparison.OrdinalIgnoreCase) ||
                 hasInsuranceSignalService ||
                 selection.HasInsuranceSignalPackage);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var checkInTime = dto.CheckInTime ?? order.CheckInTime ?? VietnamTime.Now;
                var estimatedHandover = dto.EstimatedHandover ?? order.EstimatedHandover;
                var promisedHandover = dto.PromisedHandoverTime ?? order.PromisedHandoverTime;
                await EnsureBranchOpenForCheckInAsync(order.ExecuteBranchId, checkInTime);

                if (estimatedHandover.HasValue && estimatedHandover.Value < checkInTime)
                {
                    throw new InvalidOperationException("Thời gian dự kiến hoàn thành phải lớn hơn hoặc bằng thời điểm xe vào xưởng.");
                }

                if (promisedHandover.HasValue && promisedHandover.Value < checkInTime)
                {
                    throw new InvalidOperationException("Thời gian cam kết trả xe phải lớn hơn hoặc bằng thời điểm xe vào xưởng.");
                }

                if (!string.IsNullOrWhiteSpace(normalizedOrderCategory))
                {
                    order.OrderCategory = normalizedOrderCategory;
                }
                else if (insuranceOrderContext || selection.HasInsuranceSignalPackage)
                {
                    order.OrderCategory = InsuranceBillingRules.InsuranceRepairCategoryCode;
                }
                order.ServiceAdvisorId = dto.ServiceAdvisorId;
                order.OldPartsReturnOption = NormalizeText(dto.OldPartsReturnOption) ?? order.OldPartsReturnOption;
                order.RequiredDepositAmount = Math.Max(dto.RequiredDepositAmount ?? 0m, 0m);
                order.CheckInTime = checkInTime;
                order.EstimatedHandover = estimatedHandover;
                order.PromisedHandoverTime = promisedHandover;
                order.Status = ResolveOrderStatus(dto.Status ?? order.Status);
                order.CustomerNotes = BuildSymptomsJson(dto.SymptomNotes, order.CustomerNotes);
                order.RequestedPackageIds = selection.RequestedPackageIds;
                ApplyInsuranceSnapshot(
                    order,
                    insuranceOrderContext,
                    insuranceCompanyId,
                    dto.InsuranceDeductibleAmount,
                    dto.InsuranceDeductibleOption,
                    dto.InsuranceClaimMode,
                    dto.InsuranceQuoteState);

                var now = VietnamTime.Now;
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
                    ApplyWorkshopServiceDetailValues(
                        keep,
                        serviceMap[serviceId],
                        actorStaffId,
                        now,
                        insuranceCompanyId,
                        insuranceOrderContext);
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
                    ApplyWorkshopServiceDetailValues(
                        detail,
                        service,
                        actorStaffId,
                        now,
                        insuranceCompanyId,
                        insuranceOrderContext);
                    _context.ServiceOrderDetails.Add(detail);
                }

                var subTotal = selectedServiceIds.Sum(serviceId => serviceMap[serviceId].BasePrice);
                var taxAmount = selectedServiceIds.Sum(serviceId =>
                    CalculateTaxAmount(serviceMap[serviceId].BasePrice, serviceMap[serviceId].TaxRate ?? 0m));
                order.SubTotalAmount = subTotal;
                order.TaxAmount = taxAmount;
                order.FinalAmount = subTotal + taxAmount - (order.TotalDiscount ?? 0m);

                await _context.SaveChangesAsync();
                await ReplaceServiceOrderInsuranceAssetsAsync(
                    order.Id,
                    dto.InsuranceCardImageUrls,
                    dto.InsuranceDamageImageUrls);
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

            var now = VietnamTime.Now;
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
                throw new InvalidOperationException("Dữ liệu lệnh sửa chữa không hợp lệ.");
            }

            if (item.IsQuickOrder && item.IsNewVehicle)
            {
                throw new InvalidOperationException("Không thể vừa là khách vãng lai vừa là xe mới của khách đã đăng ký.");
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
                    throw new InvalidOperationException("Vui lòng chọn khách hàng hợp lệ.");
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
                        throw new InvalidOperationException("Vui lòng chọn xe hợp lệ.");
                    }
                    await EnsureVehicleOwnershipAsync(customerId, vehicleId);
                }
            }

            var executeBranchId = item.ExecuteBranchId.GetValueOrDefault();
            var originBranchId = item.OriginBranchId.GetValueOrDefault();
            if (executeBranchId <= 0 && originBranchId <= 0)
            {
                throw new InvalidOperationException("Thiếu thông tin chi nhánh thực hiện lệnh.");
            }
            if (executeBranchId <= 0) executeBranchId = originBranchId;
            if (originBranchId <= 0) originBranchId = executeBranchId;
            var checkInTime = item.CheckInTime ?? VietnamTime.Now;
            await EnsureBranchOpenForCheckInAsync(executeBranchId, checkInTime);

            var requestedInsuranceCompanyId = item.InsuranceCompanyId.GetValueOrDefault();
            if (requestedInsuranceCompanyId > 0)
            {
                await EnsureInsuranceCompanyExistsAsync(requestedInsuranceCompanyId);
            }

            var normalizedOrderCategory = NormalizeToken(item.OrderCategory);
            if (requestedInsuranceCompanyId > 0 && string.IsNullOrWhiteSpace(normalizedOrderCategory))
            {
                normalizedOrderCategory = InsuranceBillingRules.InsuranceRepairCategoryCode;
            }

            var now = VietnamTime.Now;
            var order = new ServiceOrder
            {
                CustomerId = customerId,
                VehicleId = vehicleId,
                GroupSessionId = forcedGroupSessionId ?? item.GroupSessionId,
                OriginBranchId = originBranchId,
                ExecuteBranchId = executeBranchId,
                OrderCategory = normalizedOrderCategory,
                ServiceAdvisorId = item.ServiceAdvisorId,
                ReceptionistId = actorStaffId,
                CustomerNotes = NormalizeText(item.CustomerNotes),
                RequiredDepositAmount = item.RequiredDepositAmount,
                OldPartsReturnOption = NormalizeText(item.OldPartsReturnOption),
                CheckInTime = checkInTime,
                EstimatedHandover = item.EstimatedHandover,
                PromisedHandoverTime = item.PromisedHandoverTime,
                Status = ResolveOrderStatus(item.Status),
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId,
                CreatedAt = now,
                UpdatedAt = now
            };

            ApplyInsuranceSnapshot(
                order,
                requestedInsuranceCompanyId > 0,
                requestedInsuranceCompanyId > 0 ? requestedInsuranceCompanyId : null,
                item.InsuranceDeductibleAmount,
                item.InsuranceDeductibleOption,
                item.InsuranceClaimMode,
                item.InsuranceQuoteState);

            return order;
        }

        private async Task EnsureBranchOpenForCheckInAsync(long branchId, DateTime checkInTime)
        {
            if (branchId <= 0)
            {
                throw new InvalidOperationException("Thieu thong tin chi nhanh thuc hien lenh.");
            }

            var checkInVietnamTime = VietnamTime.ToVietnamTime(checkInTime);
            var operatingHour = await _context.BranchOperatingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == (int)checkInVietnamTime.DayOfWeek);
            if (operatingHour?.IsDayOff != true)
            {
                return;
            }

            var now = VietnamTime.Now;
            var isToday = checkInVietnamTime.Date == now.Date;
            throw new InvalidOperationException(
                isToday
                    ? "Gara không hoạt động vào hôm nay, vui lòng quay lại vào hôm sau."
                    : "Gara không hoạt động vào ngày này, vui lòng chọn ngày khác.");
        }

        private async Task<long> ResolveOrCreateQuickCustomerAsync(WorkshopServiceOrderCreateDto item, long? actorStaffId)
        {
            var guestName = NormalizeText(item.GuestName);
            if (string.IsNullOrWhiteSpace(guestName))
            {
                throw new InvalidOperationException("Vui lòng nhập họ tên khách vãng lai.");
            }

            var normalizedPhone = NormalizePhone(item.GuestPhone);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                throw new InvalidOperationException("Vui lòng nhập số điện thoại khách vãng lai.");
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
                    existing.UpdatedAt = VietnamTime.Now;
                    existing.UpdatedBy = actorStaffId;
                    await _context.SaveChangesAsync();
                }

                return existing.Id;
            }

            var now = VietnamTime.Now;
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
                throw new InvalidOperationException("Vui lòng nhập biển số xe hợp lệ.");
            }

            var modelId = rawModelId.GetValueOrDefault();
            if (modelId <= 0)
            {
                throw new InvalidOperationException("Vui lòng chọn dòng xe hợp lệ.");
            }

            if (!rawYear.HasValue || rawYear.Value <= 0)
            {
                throw new InvalidOperationException("Vui lòng chọn năm sản xuất.");
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
                    existing.UpdatedAt = VietnamTime.Now;
                    existing.UpdatedBy = actorStaffId;
                    await _context.SaveChangesAsync();
                }

                return existing.Id;
            }

            var now = VietnamTime.Now;
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
                    OwnershipStartDate = DateOnly.FromDateTime(VietnamTime.Now),
                    IsDeleted = false,
                    CreatedAt = VietnamTime.Now,
                    UpdatedAt = VietnamTime.Now,
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
                existing.UpdatedAt = VietnamTime.Now;
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
                throw new InvalidOperationException("Khách hàng đã chọn không còn hợp lệ.");
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
                throw new InvalidOperationException("Xe đã chọn không thuộc khách hàng hiện tại.");
            }
        }

        private async Task<WorkshopServiceSelection> BuildWorkshopServiceSelectionAsync(
            IEnumerable<long>? serviceIds,
            IEnumerable<long>? packageIds)
        {
            var directServiceIds = NormalizePositiveLongList(serviceIds);
            var requestedPackageIds = NormalizePositiveLongList(packageIds);
            if (directServiceIds.Count == 0 && requestedPackageIds.Count == 0)
            {
                return WorkshopServiceSelection.Empty;
            }

            var packages = requestedPackageIds.Count == 0
                ? new List<Package>()
                : await _context.Packages
                    .AsNoTracking()
                    .Include(package => package.Category)
                    .Include(package => package.PackageItems.Where(item => item.IsDeleted != true && item.IsActive != false))
                        .ThenInclude(item => item.StandardService)
                    .Where(package =>
                        requestedPackageIds.Contains(package.Id) &&
                        package.IsDeleted != true &&
                        package.IsActive != false)
                    .ToListAsync();

            if (packages.Count != requestedPackageIds.Count)
            {
                throw new InvalidOperationException("Có gói dịch vụ đã chọn không còn hợp lệ hoặc đã bị xóa.");
            }

            var packageServiceIds = packages
                .SelectMany(package => package.PackageItems ?? new List<PackageItem>())
                .Where(item =>
                    item.StandardServiceId > 0 &&
                    item.StandardService != null &&
                    item.StandardService.IsDeleted != true &&
                    item.StandardService.IsActive != false)
                .Select(item => item.StandardServiceId);

            var effectiveServiceIds = directServiceIds
                .Concat(packageServiceIds)
                .Distinct()
                .ToList();

            var serviceMap = effectiveServiceIds.Count == 0
                ? new Dictionary<long, StandardService>()
                : await _context.StandardServices
                    .AsNoTracking()
                    .Where(service =>
                        effectiveServiceIds.Contains(service.Id) &&
                        service.IsDeleted != true &&
                        service.IsActive != false)
                    .ToDictionaryAsync(service => service.Id);

            if (serviceMap.Count != effectiveServiceIds.Count)
            {
                throw new InvalidOperationException("Có dịch vụ trong gói hoặc dịch vụ đã chọn không còn hợp lệ hoặc đã bị xóa.");
            }

            return new WorkshopServiceSelection(
                directServiceIds,
                requestedPackageIds,
                effectiveServiceIds,
                serviceMap,
                packages);
        }

        private async Task AddWorkshopServiceDetailsAsync(
            ServiceOrder serviceOrder,
            WorkshopServiceSelection selection,
            long? actorStaffId,
            long? requestedInsuranceCompanyId)
        {
            if (serviceOrder == null || serviceOrder.Id <= 0)
            {
                return;
            }

            var now = VietnamTime.Now;
            var insuranceCompanyId = await ResolveOrderInsuranceCompanyIdAsync(serviceOrder, requestedInsuranceCompanyId);
            var currentOrderCategory = NormalizeToken(serviceOrder.OrderCategory);
            var hasInsuranceSignalService = selection.EffectiveServiceIds.Any(serviceId =>
                selection.ServiceMap.TryGetValue(serviceId, out var service) &&
                InsuranceBillingRules.IsInsuranceSignalService(service));
            var insuranceOrderContext = insuranceCompanyId.HasValue &&
                (string.Equals(currentOrderCategory, InsuranceBillingRules.InsuranceRepairCategoryCode, StringComparison.OrdinalIgnoreCase) ||
                 hasInsuranceSignalService ||
                 selection.HasInsuranceSignalPackage);

            if (insuranceOrderContext || selection.HasInsuranceSignalPackage)
            {
                serviceOrder.OrderCategory = InsuranceBillingRules.InsuranceRepairCategoryCode;
            }

            serviceOrder.RequestedPackageIds = selection.RequestedPackageIds;

            var details = new List<ServiceOrderDetail>(selection.EffectiveServiceIds.Count);
            foreach (var serviceId in selection.EffectiveServiceIds)
            {
                if (!selection.ServiceMap.TryGetValue(serviceId, out var service))
                {
                    continue;
                }

                var detail = new ServiceOrderDetail
                {
                    ServiceOrderId = serviceOrder.Id,
                    StandardServiceId = service.Id,
                    CreatedAt = now,
                    CreatedBy = actorStaffId,
                    UpdatedAt = now,
                    UpdatedBy = actorStaffId
                };
                ApplyWorkshopServiceDetailValues(
                    detail,
                    service,
                    actorStaffId,
                    now,
                    insuranceCompanyId,
                    insuranceOrderContext);
                details.Add(detail);
            }

            if (details.Count > 0)
            {
                _context.ServiceOrderDetails.AddRange(details);
            }

            var subTotal = details.Sum(detail => detail.SubTotal);
            var taxAmount = details.Sum(detail => detail.TaxAmount);
            serviceOrder.SubTotalAmount = subTotal;
            serviceOrder.TaxAmount = taxAmount;
            serviceOrder.FinalAmount = subTotal + taxAmount - (serviceOrder.TotalDiscount ?? 0m);
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = actorStaffId;

            await _context.SaveChangesAsync();
        }

        private void ApplyWorkshopServiceDetailValues(
            ServiceOrderDetail detail,
            StandardService service,
            long? actorStaffId,
            DateTime nowUtc,
            long? insuranceCompanyId,
            bool insuranceOrderContext)
        {
            var taxRate = service.TaxRate ?? 0m;
            var subTotal = service.BasePrice;
            var taxAmount = CalculateTaxAmount(subTotal, taxRate);
            var finalAmount = subTotal + taxAmount;

            detail.ItemType = WorkshopDetailItemType;
            detail.ItemName = service.Name;
            detail.Description = WorkshopDetailDescription;
            detail.Quantity = 1m;
            detail.Unit = WorkshopDetailUnit;
            detail.UnitPriceAtTime = service.BasePrice;
            detail.SubTotal = subTotal;
            detail.TaxRate = taxRate;
            detail.TaxAmount = taxAmount;
            detail.FinalAmount = finalAmount;
            ApplyInsuranceBilling(detail, service, insuranceCompanyId, insuranceOrderContext);
            detail.Status = WorkshopDetailStatus;
            detail.StandardServiceId = service.Id;
            detail.UpdatedAt = nowUtc;
            detail.UpdatedBy = actorStaffId;
        }

        private static void ApplyInsuranceBilling(
            ServiceOrderDetail detail,
            StandardService service,
            long? insuranceCompanyId,
            bool insuranceOrderContext)
        {
            if (InsuranceBillingRules.ShouldBillInsurance(service, insuranceOrderContext) && insuranceCompanyId.HasValue)
            {
                detail.PayerType = InsuranceBillingRules.PayerTypeInsurance;
                detail.InsuranceCompanyId = insuranceCompanyId.Value;
                detail.InsuranceRequestedAmount = detail.FinalAmount;
                detail.InsuranceApprovedAmount = 0m;
                detail.CustomerCoPayAmount = 0m;
                detail.GarageAbsorbedAmount = 0m;
                detail.InsuranceStatus = nameof(ServiceOrderDetailInsuranceStatusCode.PENDING_SUBMISSION);
                return;
            }

            detail.PayerType = InsuranceBillingRules.PayerTypeCustomer;
            detail.InsuranceCompanyId = null;
            detail.InsuranceRequestedAmount = 0m;
            detail.InsuranceApprovedAmount = 0m;
            detail.CustomerCoPayAmount = detail.FinalAmount;
            detail.GarageAbsorbedAmount = 0m;
            detail.InsuranceStatus = nameof(ServiceOrderDetailInsuranceStatusCode.NOT_APPLICABLE);
        }

        private async Task<long?> ResolveOrderInsuranceCompanyIdAsync(ServiceOrder order, long? requestedInsuranceCompanyId)
        {
            if (requestedInsuranceCompanyId.HasValue && requestedInsuranceCompanyId.Value > 0)
            {
                await EnsureInsuranceCompanyExistsAsync(requestedInsuranceCompanyId.Value);
                return requestedInsuranceCompanyId.Value;
            }

            if (!order.AppointmentId.HasValue || order.AppointmentId.Value <= 0)
            {
                return null;
            }

            var appointmentInsuranceCompanyId = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.Id == order.AppointmentId.Value)
                .Select(a => a.InsuranceCompanyId)
                .FirstOrDefaultAsync();

            if (appointmentInsuranceCompanyId.HasValue && appointmentInsuranceCompanyId.Value > 0)
            {
                await EnsureInsuranceCompanyExistsAsync(appointmentInsuranceCompanyId.Value);
                return appointmentInsuranceCompanyId.Value;
            }

            return null;
        }

        private async Task EnsureInsuranceCompanyExistsAsync(long insuranceCompanyId)
        {
            var exists = await _context.InsuranceCompanies
                .AsNoTracking()
                .AnyAsync(company => company.Id == insuranceCompanyId && company.IsDeleted != true);
            if (!exists)
            {
                throw new InvalidOperationException("Không tìm thấy công ty bảo hiểm đã chọn.");
            }
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
                    ? JsonSerializer.Serialize(new { symptoms = new[] { "Không có ghi chú gì" } })
                    : fallbackCustomerNotes;
            }

            var normalized = symptomNotes
                .Select(note => note?.Trim())
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .ToList();
            if (normalized.Count == 0)
            {
                normalized.Add("Không có ghi chú gì");
            }

            return JsonSerializer.Serialize(new { symptoms = normalized });
        }

        private static long NumberToPositiveLong(long value)
        {
            return value > 0 ? value : 0;
        }

        private static List<long> NormalizePositiveLongList(IEnumerable<long>? values)
        {
            return values?
                .Select(NumberToPositiveLong)
                .Where(value => value > 0)
                .Distinct()
                .ToList() ?? new List<long>();
        }

        private sealed class WorkshopServiceSelection
        {
            public WorkshopServiceSelection(
                List<long> directServiceIds,
                List<long> packageIds,
                List<long> effectiveServiceIds,
                Dictionary<long, StandardService> serviceMap,
                List<Package> packages)
            {
                DirectServiceIds = directServiceIds;
                PackageIds = packageIds;
                EffectiveServiceIds = effectiveServiceIds;
                ServiceMap = serviceMap;
                Packages = packages;
            }

            public static WorkshopServiceSelection Empty { get; } = new(
                new List<long>(),
                new List<long>(),
                new List<long>(),
                new Dictionary<long, StandardService>(),
                new List<Package>());

            public List<long> DirectServiceIds { get; }
            public List<long> PackageIds { get; }
            public List<long> EffectiveServiceIds { get; }
            public Dictionary<long, StandardService> ServiceMap { get; }
            public List<Package> Packages { get; }
            public bool HasSelection => DirectServiceIds.Count > 0 || PackageIds.Count > 0;
            public bool HasInsuranceSignalPackage => Packages.Any(InsuranceBillingRules.IsInsuranceSignalPackage);
            public string? RequestedPackageIds => PackageIds.Count > 0 ? string.Join(",", PackageIds) : null;
        }

        private static bool TryParsePositiveServiceOrderId(object id, out long serviceOrderId)
        {
            serviceOrderId = 0;
            if (id == null)
            {
                return false;
            }

            if (id is long longId)
            {
                serviceOrderId = longId;
                return serviceOrderId > 0;
            }

            if (id is int intId)
            {
                serviceOrderId = intId;
                return serviceOrderId > 0;
            }

            if (long.TryParse(Convert.ToString(id, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
            {
                serviceOrderId = parsedId;
                return serviceOrderId > 0;
            }

            return false;
        }


        private static bool IsWorkshopEditWindowPassed(ServiceOrder order)
        {
            if (order == null)
            {
                return false;
            }

            var normalizedStatus = NormalizeToken(order.Status) ?? string.Empty;
            return order.ActualHandover.HasValue || WorkshopMilestoneLockedStatuses.Contains(normalizedStatus);
        }
        private static string GenerateServiceOrderCode()
        {
            var timestamp = VietnamTime.Now.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture);
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
                throw new InvalidOperationException($"Trạng thái lệnh sửa chữa không hợp lệ: {status}.");
            }

            return token;
        }

        private static void ApplyInsuranceSnapshot(
            ServiceOrder order,
            bool insuranceEnabled,
            long? insuranceCompanyId,
            decimal? deductibleAmount,
            string? deductibleOption,
            string? claimMode,
            string? quoteState)
        {
            if (!insuranceEnabled || !insuranceCompanyId.HasValue || insuranceCompanyId.Value <= 0)
            {
                order.InsuranceDeductibleAmount = null;
                order.InsuranceDeductibleOption = null;
                order.InsuranceClaimMode = null;
                order.InsuranceQuoteState = null;
                return;
            }

            var normalizedDeductibleOption = NormalizeToken(deductibleOption);
            var normalizedClaimMode = NormalizeToken(claimMode);
            order.InsuranceDeductibleAmount = NormalizeInsuranceDeductibleAmount(normalizedDeductibleOption, deductibleAmount);
            order.InsuranceDeductibleOption = normalizedDeductibleOption;
            order.InsuranceClaimMode = normalizedClaimMode;
            order.InsuranceQuoteState = ResolveInsuranceQuoteState(normalizedDeductibleOption, normalizedClaimMode, quoteState);
        }

        private static decimal? NormalizeInsuranceDeductibleAmount(string? deductibleOption, decimal? rawAmount)
        {
            var normalizedOption = NormalizeToken(deductibleOption);
            if (string.Equals(normalizedOption, "500K", StringComparison.OrdinalIgnoreCase))
            {
                return 500000m;
            }

            if (string.Equals(normalizedOption, "1000K", StringComparison.OrdinalIgnoreCase))
            {
                return 1000000m;
            }

            if (string.Equals(normalizedOption, "CONSULT", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return rawAmount.HasValue && rawAmount.Value >= 0m ? rawAmount.Value : null;
        }

        private static string? ResolveInsuranceQuoteState(string? deductibleOption, string? claimMode, string? rawQuoteState)
        {
            var normalizedQuoteState = NormalizeToken(rawQuoteState);
            if (!string.IsNullOrWhiteSpace(normalizedQuoteState))
            {
                return normalizedQuoteState;
            }

            var normalizedDeductibleOption = NormalizeToken(deductibleOption);
            var normalizedClaimMode = NormalizeToken(claimMode);
            if (string.Equals(normalizedDeductibleOption, "CONSULT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedClaimMode, "ADVISOR", StringComparison.OrdinalIgnoreCase))
            {
                return "NEEDS_ADVISOR";
            }

            if (!string.IsNullOrWhiteSpace(normalizedDeductibleOption) &&
                !string.IsNullOrWhiteSpace(normalizedClaimMode))
            {
                return "PENDING_REVIEW";
            }

            return null;
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
