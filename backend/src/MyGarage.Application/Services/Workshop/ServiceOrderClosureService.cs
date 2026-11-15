using System;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class ServiceOrderClosureService : IServiceOrderClosureService
    {
        private const string CommissionTargetTypeServiceAdvisor = "SERVICE_ADVISOR";
        private const string CommissionSourceTypeServiceAdvisor = "SOADVISOR";
        private const string CommissionLogTypeEarned = "EARNED";
        private const string CommissionTypePercent = "PERCENT";
        private const string CommissionStatusPending = nameof(CommissionTransactionStatusCode.PENDING);
        private const string PaymentStatusCompleted = nameof(PaymentStatusCode.COMPLETED);

        private readonly IServiceOrderCommissionStore _commissionStore;
        private readonly IServiceOrderCommissionCommandStore _commandStore;
        private readonly IServiceOrderTransactionCommandStore _transactionStore;
        private readonly IMaintenanceReminderService _maintenanceReminderService;

        public ServiceOrderClosureService(
            IServiceOrderCommissionStore commissionStore,
            IServiceOrderCommissionCommandStore commandStore,
            IServiceOrderTransactionCommandStore transactionStore,
            IMaintenanceReminderService maintenanceReminderService)
        {
            _commissionStore = commissionStore;
            _commandStore = commandStore;
            _transactionStore = transactionStore;
            _maintenanceReminderService = maintenanceReminderService;
        }

        public async Task HandleOrderClosureAsync(
            ServiceOrder order,
            string? previousStatus,
            long? actorUserId)
        {
            await TryCreateServiceAdvisorCommissionOnClosureAsync(order, previousStatus, actorUserId);
            await TryScheduleMaintenanceReminderAsync(order, previousStatus, actorUserId);
        }

        private async Task TryScheduleMaintenanceReminderAsync(
            ServiceOrder order,
            string? previousStatus,
            long? actorUserId)
        {
            if (order == null || order.Id <= 0)
            {
                return;
            }

            if (!IsTransitionToClosedOrCompleted(previousStatus, order.Status))
            {
                return;
            }

            await _maintenanceReminderService.ScheduleFromServiceOrderAsync(
                order.Id,
                remindAfterDays: 180,
                remindAfterMileage: 5000,
                actorUserId,
                default);
        }

        private async Task TryCreateServiceAdvisorCommissionOnClosureAsync(
            ServiceOrder order,
            string? previousStatus,
            long? actorUserId)
        {
            if (order == null || order.Id <= 0)
            {
                return;
            }

            if (!IsTransitionToClosedOrCompleted(previousStatus, order.Status))
            {
                return;
            }

            if (!await HasCompletedPaymentAsync(order))
            {
                return;
            }

            var advisorStaffId = ResolveServiceAdvisorLikeStaffId(order);
            if (!advisorStaffId.HasValue)
            {
                return;
            }

            var staff = await _commissionStore.GetStaffTierInfoAsync(advisorStaffId.Value);
            if (staff == null || !staff.StaffTierId.HasValue || staff.StaffTierId.Value <= 0)
            {
                return;
            }

            var hasExisting = await _commissionStore.HasExistingAdvisorCommissionAsync(
                order.Id,
                staff.Id,
                CommissionSourceTypeServiceAdvisor,
                CommissionLogTypeEarned,
                nameof(CommissionTransactionStatusCode.CANCELED));
            if (hasExisting)
            {
                return;
            }

            var rule = await _commissionStore.GetBestMatchingCommissionRuleAsync(
                order.ExecuteBranchId,
                CommissionTargetTypeServiceAdvisor,
                staff.StaffTierId.Value,
                staff.Id);
            if (rule == null)
            {
                return;
            }

            var baseAmount = ResolveCommissionBaseAmount(order);
            if (baseAmount <= 0m)
            {
                return;
            }

            var normalizedCommissionType = ServiceOrderValueHelper.NormalizeToken(rule.CommissionType) ?? CommissionTypePercent;
            var commissionAmount = CalculateCommissionAmount(baseAmount, normalizedCommissionType, rule.CommissionValue);
            if (commissionAmount <= 0m)
            {
                return;
            }

            var now = VietnamTime.Now;
            _commandStore.AddCommissionTransaction(new CommissionTransaction
            {
                StaffId = staff.Id,
                ServiceOrderId = order.Id,
                CommissionRuleId = rule.Id,
                SourceType = CommissionSourceTypeServiceAdvisor,
                BaseAmount = baseAmount,
                AppliedCommissionType = normalizedCommissionType,
                AppliedCommissionValue = rule.CommissionValue,
                AppliedSplitRatio = 100m,
                CommissionAmount = commissionAmount,
                LogType = CommissionLogTypeEarned,
                Status = CommissionStatusPending,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorUserId,
                UpdatedBy = actorUserId
            });

            await _transactionStore.SaveChangesInTransactionAsync();
        }

        private static bool IsTransitionToClosedOrCompleted(string? previousStatus, string? currentStatus)
        {
            var prev = ServiceOrderValueHelper.NormalizeToken(previousStatus) ?? string.Empty;
            var current = ServiceOrderValueHelper.NormalizeToken(currentStatus) ?? string.Empty;

            static bool IsClosedOrCompleted(string value)
            {
                return string.Equals(value, nameof(ServiceOrderLifecycleStatus.CLOSED), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "COMPLETED", StringComparison.OrdinalIgnoreCase);
            }

            return !IsClosedOrCompleted(prev) && IsClosedOrCompleted(current);
        }

        private async Task<bool> HasCompletedPaymentAsync(ServiceOrder order)
        {
            var normalizedPaymentStatus = ServiceOrderValueHelper.NormalizeToken(order.PaymentStatus);
            if (string.Equals(normalizedPaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedPaymentStatus, PaymentStatusCompleted, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return await _commissionStore.HasCompletedPaymentRecordAsync(order.Id, PaymentStatusCompleted);
        }

        private static long? ResolveServiceAdvisorLikeStaffId(ServiceOrder order)
        {
            if (order.ServiceAdvisorId.HasValue && order.ServiceAdvisorId.Value > 0)
            {
                return order.ServiceAdvisorId.Value;
            }

            if (order.ReceptionistId.HasValue && order.ReceptionistId.Value > 0)
            {
                return order.ReceptionistId.Value;
            }

            return null;
        }

        private static decimal ResolveCommissionBaseAmount(ServiceOrder order)
        {
            var finalAmount = order.FinalAmount ?? 0m;
            if (finalAmount > 0m)
            {
                return Math.Round(finalAmount, 2, MidpointRounding.AwayFromZero);
            }

            var subTotal = order.SubTotalAmount ?? 0m;
            var tax = order.TaxAmount ?? 0m;
            var discount = ServiceOrderValueHelper.ResolveServiceOrderTotalDiscountAmount(order);
            var fallbackAmount = subTotal + tax - discount;
            return fallbackAmount > 0m
                ? Math.Round(fallbackAmount, 2, MidpointRounding.AwayFromZero)
                : 0m;
        }

        private static decimal CalculateCommissionAmount(decimal baseAmount, string commissionType, decimal commissionValue)
        {
            if (baseAmount <= 0m || commissionValue <= 0m)
            {
                return 0m;
            }

            if (string.Equals(commissionType, CommissionTypePercent, StringComparison.OrdinalIgnoreCase))
            {
                return Math.Round(baseAmount * commissionValue / 100m, 2, MidpointRounding.AwayFromZero);
            }

            return Math.Round(commissionValue, 2, MidpointRounding.AwayFromZero);
        }
    }
}
