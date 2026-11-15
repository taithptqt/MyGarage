using System;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    /// <summary>
    /// Encapsulates workshop intake checks, branch operating-day guards, and quick customer/vehicle provisioning.
    /// </summary>
    public sealed class ServiceOrderIntakeService : IServiceOrderIntakeService
    {
        private readonly IServiceOrderStore _store;

        private const string CheckInLogType = "CHECK_IN";
        private const string CheckInPendingStatus = nameof(DiagnosisStatus.PENDING);
        // Keep guard enabled: check-in/order creation must follow branch operating schedule.
        private static readonly bool BypassBranchDayOffCheckInGuard = false;

        public ServiceOrderIntakeService(IServiceOrderStore store)
        {
            _store = store;
        }

        public async Task EnsureCheckInRequestExistsAsync(long serviceOrderId)
        {
            if (serviceOrderId <= 0)
            {
                return;
            }

            var hasAnyCheckInLog = await _store.HasCheckInConditionLogAsync(serviceOrderId, CheckInLogType);
            if (hasAnyCheckInLog)
            {
                return;
            }

            var now = VietnamTime.Now;
            _store.AddVehicleConditionLog(new VehicleConditionLog
            {
                ServiceOrderId = serviceOrderId,
                LogType = CheckInLogType,
                DashboardWarningLights = CheckInPendingStatus,
                CreatedAt = now,
                UpdatedAt = now
            });

            await _store.SaveChangesInTransactionAsync();
        }

        public async Task<ServiceOrder> PrepareWorkshopOrderEntityAsync(
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

            var normalizedOrderCategory = ServiceOrderService.NormalizeToken(item.OrderCategory);
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
                ServiceAdvisorId = ResolveServiceAdvisorForWorkshopCreate(item.ServiceAdvisorId, actorStaffId),
                ReceptionistId = actorStaffId,
                CustomerNotes = CustomerNoteFormatter.ToDisplayText(item.CustomerNotes),
                RequiredDepositAmount = item.RequiredDepositAmount,
                OldPartsReturnOption = ServiceOrderService.NormalizeText(item.OldPartsReturnOption),
                CheckInTime = checkInTime,
                EstimatedHandover = item.EstimatedHandover,
                PromisedHandoverTime = item.PromisedHandoverTime,
                Status = ServiceOrderService.ResolveOrderStatus(item.Status),
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId,
                CreatedAt = now,
                UpdatedAt = now
            };

            ServiceOrderService.ApplyInsuranceSnapshot(
                order,
                requestedInsuranceCompanyId > 0,
                requestedInsuranceCompanyId > 0 ? requestedInsuranceCompanyId : null,
                item.InsuranceDeductibleAmount,
                item.InsuranceDeductibleOption,
                item.InsuranceClaimMode,
                item.InsuranceQuoteState);

            return order;
        }

        public async Task EnsureBranchOpenForCheckInAsync(long branchId, DateTime checkInTime)
        {
            if (BypassBranchDayOffCheckInGuard)
            {
                return;
            }

            if (branchId <= 0)
            {
                throw new InvalidOperationException("Thiếu thông tin chi nhánh thực hiện lệnh.");
            }

            var checkInVietnamTime = VietnamTime.ToVietnamTime(checkInTime);
            var isDayOff = await _store.IsBranchDayOffAsync(branchId, (int)checkInVietnamTime.DayOfWeek);
            if (!isDayOff)
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

        public async Task EnsureCustomerExistsAsync(long customerId)
        {
            var exists = await _store.CustomerExistsAsync(customerId);
            if (!exists)
            {
                throw new InvalidOperationException("Khách hàng đã chọn không còn hợp lệ.");
            }
        }

        public async Task EnsureInsuranceCompanyExistsAsync(long insuranceCompanyId)
        {
            var exists = await _store.ActiveInsuranceCompanyExistsAsync(insuranceCompanyId);
            if (!exists)
            {
                throw new InvalidOperationException("Không tìm thấy công ty bảo hiểm đã chọn.");
            }
        }

        private async Task<long> ResolveOrCreateQuickCustomerAsync(WorkshopServiceOrderCreateDto item, long? actorStaffId)
        {
            var guestName = ServiceOrderService.NormalizeText(item.GuestName);
            if (string.IsNullOrWhiteSpace(guestName))
            {
                throw new InvalidOperationException("Vui lòng nhập họ tên khách vãng lai.");
            }

            var normalizedPhone = NormalizePhone(item.GuestPhone);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                throw new InvalidOperationException("Vui lòng nhập số điện thoại khách vãng lai.");
            }

            var existing = await _store.FindActiveCustomerByNormalizedPhoneAsync(normalizedPhone);

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
                    await _store.SaveChangesInTransactionAsync();
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

            _store.AddCustomer(created);
            await _store.SaveChangesInTransactionAsync();
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

            var existing = await _store.FindVehicleByLicensePlateAsync(licensePlate);
            if (existing != null)
            {
                var changed = false;
                var normalizedColor = ServiceOrderService.NormalizeText(rawColor);
                var normalizedVin = ServiceOrderService.NormalizeText(rawVin);

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
                    await _store.SaveChangesInTransactionAsync();
                }

                return existing.Id;
            }

            var now = VietnamTime.Now;
            var created = new Vehicle
            {
                LicensePlate = licensePlate,
                ModelId = modelId,
                Color = ServiceOrderService.NormalizeText(rawColor),
                Vin = ServiceOrderService.NormalizeText(rawVin),
                ManufactureYear = rawYear.HasValue && rawYear.Value > 0 ? rawYear.Value : null,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId,
                IsDeleted = false
            };

            _store.AddVehicle(created);
            await _store.SaveChangesInTransactionAsync();
            return created.Id;
        }

        private async Task EnsureCustomerVehicleLinkForOrderAsync(long customerId, long vehicleId, long? actorStaffId)
        {
            var existing = await _store.FindCustomerVehicleAsync(customerId, vehicleId);

            if (existing == null)
            {
                _store.AddCustomerVehicle(new CustomerVehicle
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
                await _store.SaveChangesInTransactionAsync();
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
                await _store.SaveChangesInTransactionAsync();
            }
        }

        private async Task EnsureVehicleOwnershipAsync(long customerId, long vehicleId)
        {
            var owned = await _store.VehicleOwnedByCustomerAsync(customerId, vehicleId);
            if (!owned)
            {
                throw new InvalidOperationException("Xe đã chọn không thuộc khách hàng hiện tại.");
            }
        }

        private static long? ResolveServiceAdvisorForWorkshopCreate(long? requestedServiceAdvisorId, long? actorStaffId)
        {
            if (requestedServiceAdvisorId.HasValue && requestedServiceAdvisorId.Value > 0)
            {
                return requestedServiceAdvisorId.Value;
            }

            if (actorStaffId.HasValue && actorStaffId.Value > 0)
            {
                return actorStaffId.Value;
            }

            return null;
        }

        private static string NormalizePhone(string? value)
        {
            return string.Concat((value ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch))).Trim();
        }

        private static string NormalizeLicensePlate(string? rawLicensePlate)
        {
            return string.Concat((rawLicensePlate ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Where(ch => !char.IsWhiteSpace(ch)));
        }
    }
}
