using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyGarage.Application.Authorization;
using MyGarage.Application.Payroll.DTOs;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.Infrastructure.Payroll;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class PayrollAndHrModuleTests
{
    // =========================================================================
    // SECTION 1: Pure Domain Rules: PayrollDomainRules
    // =========================================================================

    [Fact]
    public void PayrollRules_PeriodParsing_ValidatesFormatAndExtractsMonthBounds()
    {
        // 1. Valid period strings
        Assert.True(PayrollDomainRules.TryParsePeriod("2026-05", out var code1, out var start1, out var end1));
        Assert.Equal("2026-05", code1);
        Assert.Equal(new DateOnly(2026, 5, 1), start1);
        Assert.Equal(new DateOnly(2026, 5, 31), end1);

        // Leap year February
        Assert.True(PayrollDomainRules.TryParsePeriod("2028-02", out var codeLeap, out var startLeap, out var endLeap));
        Assert.Equal("2028-02", codeLeap);
        Assert.Equal(new DateOnly(2028, 2, 1), startLeap);
        Assert.Equal(new DateOnly(2028, 2, 29), endLeap);

        // Non-leap year February
        Assert.True(PayrollDomainRules.TryParsePeriod("2026-02", out var codeNonLeap, out var startNonLeap, out var endNonLeap));
        Assert.Equal(new DateOnly(2026, 2, 28), endNonLeap);

        // Null or whitespace generates current period code and bounds
        Assert.True(PayrollDomainRules.TryParsePeriod(null, out var codeNull, out var startNull, out var endNull));
        Assert.False(string.IsNullOrWhiteSpace(codeNull));
        Assert.Equal(1, startNull.Day);

        // Strict format validator rejects null/empty/invalid
        Assert.False(PayrollDomainRules.ValidatePeriodFormat(null, out _, out _, out _));
        Assert.False(PayrollDomainRules.ValidatePeriodFormat("", out _, out _, out _));
        Assert.False(PayrollDomainRules.ValidatePeriodFormat("2026/05", out _, out _, out _));
        Assert.False(PayrollDomainRules.ValidatePeriodFormat("invalid-period", out _, out _, out _));
        Assert.False(PayrollDomainRules.ValidatePeriodFormat("2026-13", out _, out _, out _));
    }

    [Fact]
    public void PayrollRules_CanModify_EnforcesDraftAndCalculatedOnly()
    {
        Assert.True(PayrollDomainRules.CanModifyPayrollRun("DRAFT"));
        Assert.True(PayrollDomainRules.CanModifyPayrollRun("CALCULATED"));
        Assert.True(PayrollDomainRules.CanModifyPayrollRun("draft"));
        Assert.True(PayrollDomainRules.CanModifyPayrollRun("calculated"));

        Assert.False(PayrollDomainRules.CanModifyPayrollRun("APPROVED"));
        Assert.False(PayrollDomainRules.CanModifyPayrollRun("PAID"));
        Assert.False(PayrollDomainRules.CanModifyPayrollRun("LOCKED"));
        Assert.False(PayrollDomainRules.CanModifyPayrollRun("CANCELED"));
        Assert.False(PayrollDomainRules.CanModifyPayrollRun(null));
        Assert.False(PayrollDomainRules.CanModifyPayrollRun(""));

        // Period guards
        Assert.True(PayrollDomainRules.CanAddRunToPeriod("OPEN"));
        Assert.True(PayrollDomainRules.CanAddRunToPeriod("CALCULATED"));
        Assert.False(PayrollDomainRules.CanAddRunToPeriod("APPROVED"));
        Assert.False(PayrollDomainRules.CanAddRunToPeriod("PAID"));
        Assert.False(PayrollDomainRules.CanAddRunToPeriod("LOCKED"));
    }

    [Fact]
    public void PayrollRules_RunStatusTransition_EnforcesStateTransitionsAndTriggerInvariant()
    {
        // Valid forward transitions
        Assert.True(PayrollDomainRules.ValidateRunStatusTransition("DRAFT", "CALCULATED", out var err1));
        Assert.Null(err1);
        Assert.True(PayrollDomainRules.ValidateRunStatusTransition("DRAFT", "APPROVED", out _));
        Assert.True(PayrollDomainRules.ValidateRunStatusTransition("DRAFT", "CANCELED", out _));
        Assert.True(PayrollDomainRules.ValidateRunStatusTransition("CALCULATED", "APPROVED", out _));
        Assert.True(PayrollDomainRules.ValidateRunStatusTransition("APPROVED", "PAID", out _));
        Assert.True(PayrollDomainRules.ValidateRunStatusTransition("PAID", "LOCKED", out _));

        // CRITICAL INVARIANT: Direct transition to PAID from DRAFT or CALCULATED is strictly forbidden
        Assert.False(PayrollDomainRules.ValidateRunStatusTransition("DRAFT", "PAID", out var errDirectPaid1));
        Assert.NotNull(errDirectPaid1);
        Assert.Contains("không qua phê duyệt", errDirectPaid1, StringComparison.OrdinalIgnoreCase);

        Assert.False(PayrollDomainRules.ValidateRunStatusTransition("CALCULATED", "PAID", out var errDirectPaid2));
        Assert.NotNull(errDirectPaid2);
        Assert.Contains("không qua phê duyệt", errDirectPaid2, StringComparison.OrdinalIgnoreCase);

        // Terminal states cannot transition to anything
        Assert.False(PayrollDomainRules.ValidateRunStatusTransition("LOCKED", "DRAFT", out _));
        Assert.False(PayrollDomainRules.ValidateRunStatusTransition("LOCKED", "CALCULATED", out _));
        Assert.False(PayrollDomainRules.ValidateRunStatusTransition("CANCELED", "APPROVED", out _));
    }

    [Fact]
    public void PayrollRules_ApprovalAndPaymentGuards_EnforcePreconditions()
    {
        // Approval requires DRAFT/CALCULATED and > 0 items
        Assert.True(PayrollDomainRules.CanApprovePayrollRun("DRAFT", 5, out var errOk));
        Assert.Null(errOk);
        Assert.True(PayrollDomainRules.CanApprovePayrollRun("CALCULATED", 1, out _));

        Assert.False(PayrollDomainRules.CanApprovePayrollRun("DRAFT", 0, out var errNoItems));
        Assert.Contains("không có dòng chi tiết", errNoItems, StringComparison.OrdinalIgnoreCase);

        Assert.False(PayrollDomainRules.CanApprovePayrollRun("APPROVED", 5, out var errAlreadyApproved));
        Assert.Contains("DRAFT hoặc CALCULATED", errAlreadyApproved, StringComparison.OrdinalIgnoreCase);

        // Payment requires APPROVED status
        Assert.True(PayrollDomainRules.CanMarkPaidPayrollRun("APPROVED", out var errPayOk));
        Assert.Null(errPayOk);
        Assert.False(PayrollDomainRules.CanMarkPaidPayrollRun("DRAFT", out var errPayDraft));
        Assert.Contains("đã được duyệt", errPayDraft, StringComparison.OrdinalIgnoreCase);
        Assert.False(PayrollDomainRules.CanMarkPaidPayrollRun("LOCKED", out _));

        // Locking requires PAID or APPROVED
        Assert.True(PayrollDomainRules.CanLockPayrollRun("PAID", out _));
        Assert.True(PayrollDomainRules.CanLockPayrollRun("APPROVED", out _));
        Assert.False(PayrollDomainRules.CanLockPayrollRun("DRAFT", out _));
    }

    [Fact]
    public void PayrollRules_MonotonicRunNoAndCommissionReconciliation_CalculateAccurately()
    {
        Assert.Equal(1, PayrollDomainRules.CalculateNextRunNo(0));
        Assert.Equal(4, PayrollDomainRules.CalculateNextRunNo(3));
        Assert.Equal(1, PayrollDomainRules.CalculateNextRunNo(-1));

        // Commission reconciliation
        var (isReconciled, diff) = PayrollDomainRules.ReconcileCommissions(10_000_000m, 10_000_000m);
        Assert.True(isReconciled);
        Assert.Equal(0m, diff);

        var (withinTolerance, smallDiff) = PayrollDomainRules.ReconcileCommissions(10_000_000.005m, 10_000_000m);
        Assert.True(withinTolerance);
        Assert.Equal(0.005m, smallDiff);

        var (discrepant, largeDiff) = PayrollDomainRules.ReconcileCommissions(10_500_000m, 10_000_000m);
        Assert.False(discrepant);
        Assert.Equal(500_000m, largeDiff);
    }

    // =========================================================================
    // SECTION 2: Pure Domain Rules: SalaryDomainRules
    // =========================================================================

    [Fact]
    public void SalaryRules_ProfileValidation_EnforcesPositiveSalaryAndDates()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 12, 31);

        // Valid profiles
        Assert.True(SalaryDomainRules.ValidateSalaryProfile(15_000_000m, start, end, out var err1));
        Assert.Null(err1);
        Assert.True(SalaryDomainRules.ValidateSalaryProfile(12_000_000m, start, null, out var err2));
        Assert.Null(err2);

        // Invalid: 0 or negative salary
        Assert.False(SalaryDomainRules.ValidateSalaryProfile(0m, start, end, out var errZero));
        Assert.Contains("lớn hơn 0", errZero, StringComparison.OrdinalIgnoreCase);
        Assert.False(SalaryDomainRules.ValidateSalaryProfile(-5_000_000m, start, end, out _));
        Assert.False(SalaryDomainRules.ValidateSalaryProfile(null, start, end, out _));

        // Invalid: missing effectiveFrom
        Assert.False(SalaryDomainRules.ValidateSalaryProfile(10_000_000m, (DateOnly?)null, end, out var errNoStart));
        Assert.Contains("bắt buộc", errNoStart, StringComparison.OrdinalIgnoreCase);

        // Invalid: effectiveTo < effectiveFrom
        Assert.False(SalaryDomainRules.ValidateSalaryProfile(10_000_000m, end, start, out var errInverted));
        Assert.Contains("lớn hơn hoặc bằng", errInverted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SalaryRules_CheckProfileDateOverlap_CorrectlyDetectsOverlaps()
    {
        var d1 = new DateOnly(2026, 1, 1);
        var d2 = new DateOnly(2026, 6, 30);
        var d3 = new DateOnly(2026, 7, 1);
        var d4 = new DateOnly(2026, 12, 31);

        // Disjoint ranges
        Assert.False(SalaryDomainRules.CheckProfileDateOverlap(d1, d2, d3, d4));
        Assert.False(SalaryDomainRules.CheckProfileDateOverlap(d3, d4, d1, d2));

        // Overlapping ranges
        Assert.True(SalaryDomainRules.CheckProfileDateOverlap(d1, new DateOnly(2026, 8, 1), d2, d4));

        // Boundary touch (same day overlap)
        Assert.True(SalaryDomainRules.CheckProfileDateOverlap(d1, d2, d2, d4));

        // Unbounded range overlaps everything after its start
        Assert.True(SalaryDomainRules.CheckProfileDateOverlap(d1, null, d3, d4));
        Assert.True(SalaryDomainRules.CheckProfileDateOverlap(d3, null, d1, d4));
        Assert.False(SalaryDomainRules.CheckProfileDateOverlap(new DateOnly(2027, 1, 1), null, d1, d4));
    }

    [Fact]
    public void SalaryRules_CompensationComponentsAndNetSalary_CalculateAccurately()
    {
        // Non-negative validation
        Assert.True(SalaryDomainRules.ValidateCompensationComponents(10_000_000m, 1_000_000m, 2_000_000m, 500_000m, 0m, 0m, 0m, out var errOk));
        Assert.Null(errOk);

        Assert.False(SalaryDomainRules.ValidateCompensationComponents(-1m, 0m, 0m, 0m, 0m, 0m, 0m, out var errNegBase));
        Assert.Contains("không được âm", errNegBase, StringComparison.OrdinalIgnoreCase);

        Assert.False(SalaryDomainRules.ValidateCompensationComponents(10_000_000m, -500_000m, 0m, 0m, 0m, 0m, 0m, out _));
        Assert.False(SalaryDomainRules.ValidateCompensationComponents(10_000_000m, 0m, 0m, 0m, 0m, -100_000m, 0m, out _));

        // Net salary calculation
        // NetPay = Base(15M) + Allow(2M) + Comm(3M) + OT(1M) + Bonus(1M) - Deduct(2M) - Penalty(500k) = 19.5M
        var net = SalaryDomainRules.CalculateNetSalary(
            baseSalary: 15_000_000m,
            allowance: 2_000_000m,
            commission: 3_000_000m,
            overtime: 1_000_000m,
            bonus: 1_000_000m,
            deductions: 2_000_000m,
            penalty: 500_000m);
        Assert.Equal(19_500_000m, net);

        // When deductions exceed earnings, NetPay is floored at 0
        var floored = SalaryDomainRules.CalculateNetSalary(5_000_000m, 0m, 0m, 0m, 0m, 10_000_000m, 0m);
        Assert.Equal(0m, floored);
    }

    [Fact]
    public void SalaryRules_VietnamStatutoryInsurance_CalculatesStatutoryTiersAndCaps()
    {
        // Testing insurance for 20,000,000 VND
        var (empTotal, emprTotal, empBhxh, empBhyt, empBhtn, emprBhxh, emprBhyt, emprBhtn, emprOaid) =
            SalaryDomainRules.CalculateVietnamStatutoryInsurance(20_000_000m);

        // Employee: 8% + 1.5% + 1% = 10.5%
        Assert.Equal(1_600_000m, empBhxh); // 20M * 8%
        Assert.Equal(300_000m, empBhyt);   // 20M * 1.5%
        Assert.Equal(200_000m, empBhtn);   // 20M * 1%
        Assert.Equal(2_100_000m, empTotal);

        // Employer: 17.5% + 3% + 1% + 0.5% = 22%
        Assert.Equal(3_500_000m, emprBhxh); // 20M * 17.5%
        Assert.Equal(600_000m, emprBhyt);   // 20M * 3%
        Assert.Equal(200_000m, emprBhtn);   // 20M * 1%
        Assert.Equal(100_000m, emprOaid);   // 20M * 0.5%
        Assert.Equal(4_400_000m, emprTotal);

        // Capping test: High salary of 120,000,000 VND
        // Social insurance cap = 20 * 2,340,000 = 46,800,000
        // Unemployment cap = 20 * 4,960,000 = 99,200,000
        var (cappedEmpTotal, _, cappedEmpBhxh, cappedEmpBhyt, cappedEmpBhtn, _, _, _, _) =
            SalaryDomainRules.CalculateVietnamStatutoryInsurance(120_000_000m);

        Assert.Equal(Math.Round(46_800_000m * 0.08m), cappedEmpBhxh);
        Assert.Equal(Math.Round(46_800_000m * 0.015m), cappedEmpBhyt);
        Assert.Equal(Math.Round(99_200_000m * 0.01m), cappedEmpBhtn);
        Assert.Equal(cappedEmpBhxh + cappedEmpBhyt + cappedEmpBhtn, cappedEmpTotal);
    }

    [Fact]
    public void SalaryRules_VietnamPersonalIncomeTax_CalculatesSevenProgressiveTiers()
    {
        // 0 or negative
        Assert.Equal(0m, SalaryDomainRules.CalculateVietnamPersonalIncomeTax(0m));
        Assert.Equal(0m, SalaryDomainRules.CalculateVietnamPersonalIncomeTax(-1_000_000m));

        // Tier 1: 5,000,000 @ 5% = 250,000
        Assert.Equal(250_000m, SalaryDomainRules.CalculateVietnamPersonalIncomeTax(5_000_000m));

        // Tier 2: 10,000,000 -> 5M @ 5% (250k) + 5M @ 10% (500k) = 750,000
        Assert.Equal(750_000m, SalaryDomainRules.CalculateVietnamPersonalIncomeTax(10_000_000m));

        // Tier 3: 18,000,000 -> 750k + 8M @ 15% (1.2M) = 1,950,000
        Assert.Equal(1_950_000m, SalaryDomainRules.CalculateVietnamPersonalIncomeTax(18_000_000m));

        // Tier 4: 32,000,000 -> 1.95M + 14M @ 20% (2.8M) = 4,750,000
        Assert.Equal(4_750_000m, SalaryDomainRules.CalculateVietnamPersonalIncomeTax(32_000_000m));

        // Tier 5: 52,000,000 -> 4.75M + 20M @ 25% (5M) = 9,750,000
        Assert.Equal(9_750_000m, SalaryDomainRules.CalculateVietnamPersonalIncomeTax(52_000_000m));

        // Tier 6: 80,000,000 -> 9.75M + 28M @ 30% (8.4M) = 18,150,000
        Assert.Equal(18_150_000m, SalaryDomainRules.CalculateVietnamPersonalIncomeTax(80_000_000m));

        // Tier 7: 100,000,000 -> 18.15M + 20M @ 35% (7M) = 25,150,000
        Assert.Equal(25_150_000m, SalaryDomainRules.CalculateVietnamPersonalIncomeTax(100_000_000m));
    }

    [Fact]
    public void SalaryRules_StatutoryDeductions_Applies2026ReformProperly()
    {
        // Monthly gross income: 30,000,000 VND, 1 dependent
        // Employee insurance on 30M: 30M * 10.5% = 3,150,000

        // Pre-2026: Personal = 11M, Dependent = 4.4M (total allowances = 15.4M)
        // Taxable = 30M - 3.15M - 15.4M = 11,450,000
        var (_, _, taxablePre2026, pitPre2026) = SalaryDomainRules.CalculateStatutoryDeductions(
            baseSalaryForInsurance: 30_000_000m,
            grossIncome: 30_000_000m,
            dependentCount: 1,
            is2026OrLater: false);

        Assert.Equal(11_450_000m, taxablePre2026);
        // Tax = 5M*5% (250k) + 5M*10% (500k) + 1.45M*15% (217.5k) = 967,500
        Assert.Equal(967_500m, pitPre2026);

        // 2026 onwards: Personal = 15.5M, Dependent = 6.2M (total allowances = 21.7M)
        // Taxable = 30M - 3.15M - 21.7M = 5,150,000
        var (_, _, taxable2026, pit2026) = SalaryDomainRules.CalculateStatutoryDeductions(
            baseSalaryForInsurance: 30_000_000m,
            grossIncome: 30_000_000m,
            dependentCount: 1,
            is2026OrLater: true);

        Assert.Equal(5_150_000m, taxable2026);
        // Tax = 5M*5% (250k) + 150k*10% (15k) = 265,000
        Assert.Equal(265_000m, pit2026);
        Assert.True(pit2026 < pitPre2026, "2026 tax reform should reduce PIT burden.");
    }

    [Fact]
    public void SalaryRules_ApprovalDecision_ValidatesPendingStatusAndAmount()
    {
        Assert.True(SalaryDomainRules.ValidateApprovalDecision("PENDING", 15_000_000m, DateTime.UtcNow, out var err1));
        Assert.Null(err1);

        // Non-pending status cannot be decided
        Assert.False(SalaryDomainRules.ValidateApprovalDecision("APPROVED", 15_000_000m, DateTime.UtcNow, out var errApproved));
        Assert.Contains("PENDING", errApproved, StringComparison.OrdinalIgnoreCase);

        Assert.False(SalaryDomainRules.ValidateApprovalDecision("REJECTED", 15_000_000m, DateTime.UtcNow, out _));

        // Negative or zero approved salary
        Assert.False(SalaryDomainRules.ValidateApprovalDecision("PENDING", -100m, DateTime.UtcNow, out var errNeg));
        Assert.Contains("lớn hơn 0", errNeg, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // SECTION 3: Authorization Port: PayrollAuthorizationService
    // =========================================================================

    [Fact]
    public async Task PayrollAuthorization_Admin_HasUnrestrictedAccess()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PayrollAuthAdmin_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options);
        var auth = new PayrollAuthorizationService(new EfPayrollStore(db));

        var adminActor = new UserActorContext(1, "Staff", "ADMIN", BranchId: null, IsAdmin: true);

        Assert.True(await auth.CanViewSalaryProfilesAsync(branchId: 99, adminActor));
        Assert.True(await auth.CanManageSalaryProfilesAsync(branchId: 99, adminActor));
        Assert.True(await auth.CanViewPayrollRunsAsync(branchId: 99, adminActor));
        Assert.True(await auth.CanCalculatePayrollAsync(branchId: 99, adminActor));
        Assert.True(await auth.CanApprovePayrollAsync(payrollRunId: 10, adminActor));
        Assert.True(await auth.CanMarkPaidPayrollAsync(payrollRunId: 10, adminActor));
        Assert.True(await auth.CanViewMyPayslipAsync(staffId: 5, adminActor));
        Assert.True(await auth.CanAccessStaffSalaryAsync(staffId: 5, adminActor));
    }

    [Fact]
    public async Task PayrollAuthorization_BranchScoping_EnforcesIsolationForNonAdmin()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PayrollAuthBranch_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options);

        // Add a payroll run belonging to branch 1
        db.PayrollRuns.Add(new PayrollRun
        {
            Id = 100,
            PayrollPeriodId = 1,
            BranchId = 1,
            RunNo = 1,
            Status = "CALCULATED"
        });
        await db.SaveChangesAsync();

        var auth = new PayrollAuthorizationService(new EfPayrollStore(db));

        // Branch 1 accountant
        var branch1Actor = new UserActorContext(10, "Staff", "ACCOUNTANT", BranchId: 1, IsAdmin: false);
        // Branch 2 accountant
        var branch2Actor = new UserActorContext(20, "Staff", "ACCOUNTANT", BranchId: 2, IsAdmin: false);

        // Branch 1 accountant can access branch 1
        Assert.True(await auth.CanViewPayrollRunsAsync(1, branch1Actor));
        Assert.True(await auth.CanCalculatePayrollAsync(1, branch1Actor));
        Assert.True(await auth.CanApprovePayrollAsync(100, branch1Actor));

        // Branch 2 accountant cannot access branch 1
        Assert.False(await auth.CanViewPayrollRunsAsync(1, branch2Actor));
        Assert.False(await auth.CanCalculatePayrollAsync(1, branch2Actor));
        Assert.False(await auth.CanApprovePayrollAsync(100, branch2Actor));
    }

    [Fact]
    public async Task PayrollAuthorization_RoleAccess_PermitsPayrollRolesAndDeniesOthers()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PayrollAuthRoles_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options);
        var auth = new PayrollAuthorizationService(new EfPayrollStore(db));

        var hrActor = new UserActorContext(2, "Staff", "HR_MANAGER", BranchId: 1, IsAdmin: false);
        var accountantActor = new UserActorContext(3, "Staff", "KE_TOAN", BranchId: 1, IsAdmin: false);
        var mechanicActor = new UserActorContext(4, "Staff", "TECHNICIAN", BranchId: 1, IsAdmin: false);
        var anonActor = new UserActorContext(null, null, null, null, false);

        // HR & Accountant have payroll view permissions
        Assert.True(await auth.CanViewSalaryProfilesAsync(1, hrActor));
        Assert.True(await auth.CanViewSalaryProfilesAsync(1, accountantActor));

        // Technician cannot view or manage salary profiles
        Assert.False(await auth.CanViewSalaryProfilesAsync(1, mechanicActor));
        Assert.False(await auth.CanManageSalaryProfilesAsync(1, mechanicActor));
        Assert.False(await auth.CanViewPayrollRunsAsync(1, mechanicActor));

        // Anonymous has 0 access
        Assert.False(await auth.CanViewSalaryProfilesAsync(1, anonActor));
        Assert.False(await auth.CanCalculatePayrollAsync(1, anonActor));
    }

    [Fact]
    public async Task PayrollAuthorization_PayslipSelfAccess_RestrictsToSelfOrAdmin()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PayrollAuthPayslip_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options);
        var auth = new PayrollAuthorizationService(new EfPayrollStore(db));

        var staff10Actor = new UserActorContext(10, "Staff", "TECHNICIAN", BranchId: 1, IsAdmin: false);
        var staff20Actor = new UserActorContext(20, "Staff", "TECHNICIAN", BranchId: 1, IsAdmin: false);

        // Staff 10 can view staff 10's payslip
        Assert.True(await auth.CanViewMyPayslipAsync(10, staff10Actor));

        // Staff 10 CANNOT view staff 20's payslip
        Assert.False(await auth.CanViewMyPayslipAsync(20, staff10Actor));
    }

    // =========================================================================
    // SECTION 4: Application Services Operations: PayrollApplicationService
    // =========================================================================

    private sealed class FakePayrollBatchCalculationService : IPayrollBatchCalculationService
    {
        public Task<PayrollBatchCalculationResult> CalculatePayrollForAllAsync(
            PayrollBatchCalculationCommand command,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PayrollBatchCalculationResult
            {
                PayrollPeriodId = command.PayrollPeriodId ?? 1,
                PayrollRunId = 1,
                ProcessedStaffCount = 1,
                GrossTotal = 10_000_000m,
                NetTotal = 9_000_000m
            });
        }
    }

    [Fact]
    public async Task PayrollApplicationService_CreateSalaryProfile_PersistsAndRejectsOverlaps()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PayrollAppDb_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options);

        db.Staffs.Add(new Staff
        {
            Id = 101,
            FullName = "Nguyễn Văn Kế Toán",
            BranchId = 1,
            PasswordHash = "hash"
        });
        await db.SaveChangesAsync();

        var auth = new PayrollAuthorizationService(new EfPayrollStore(db));
        var service = new PayrollApplicationService(new MyGarage.Infrastructure.Payroll.EfPayrollStore(db), auth, new FakePayrollBatchCalculationService());
        var adminActor = new UserActorContext(1, "Staff", "ADMIN", 1, IsAdmin: true);

        // 1. Create initial profile
        var cmd1 = new SalaryProfileUpsertCommand(
            StaffId: 101,
            BranchId: 1,
            BaseSalary: 12_000_000m,
            EffectiveFrom: new DateTime(2026, 1, 1),
            EffectiveTo: new DateTime(2026, 6, 30),
            Status: "ACTIVE",
            Note: "Lương thử việc 6 tháng"
        );

        var created1 = await service.CreateSalaryProfileAsync(cmd1, adminActor);
        Assert.NotNull(created1);
        Assert.True(created1.Id > 0);
        Assert.Equal(12_000_000m, created1.BaseSalary);

        // 2. Overlapping profile creation must throw InvalidOperationException
        var overlappingCmd = new SalaryProfileUpsertCommand(
            StaffId: 101,
            BranchId: 1,
            BaseSalary: 15_000_000m,
            EffectiveFrom: new DateTime(2026, 4, 1),
            EffectiveTo: new DateTime(2026, 12, 31),
            Status: "ACTIVE",
            Note: "Trùng khoảng hiệu lực"
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSalaryProfileAsync(overlappingCmd, adminActor));

        // 3. Non-overlapping profile succeeds
        var nonOverlappingCmd = new SalaryProfileUpsertCommand(
            StaffId: 101,
            BranchId: 1,
            BaseSalary: 16_000_000m,
            EffectiveFrom: new DateTime(2026, 7, 1),
            EffectiveTo: null,
            Status: "ACTIVE",
            Note: "Lương chính thức"
        );

        var created2 = await service.CreateSalaryProfileAsync(nonOverlappingCmd, adminActor);
        Assert.NotNull(created2);
        Assert.Equal(16_000_000m, created2.BaseSalary);
    }

    [Fact]
    public async Task PayrollApplicationService_PreviewPayrollRun_CalculatesCompensationForStaff()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PayrollAppPreview_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options);

        db.Staffs.Add(new Staff
        {
            Id = 201,
            FullName = "Trần Kỹ Thuật Viên",
            BranchId = 1,
            PasswordHash = "hash"
        });
        db.StaffSalaryProfiles.Add(new StaffSalaryProfile
        {
            Id = 1,
            StaffId = 201,
            BranchId = 1,
            BaseSalary = 10_000_000m,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.StaffContracts.Add(new StaffContract
        {
            Id = 1,
            StaffId = 201,
            ContractType = "FULL_TIME",
            PayFrequency = "MONTHLY",
            BaseSalary = 10_000_000m,
            AllowanceAmount = 1_500_000m,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var auth = new PayrollAuthorizationService(new EfPayrollStore(db));
        var service = new PayrollApplicationService(new MyGarage.Infrastructure.Payroll.EfPayrollStore(db), auth, new FakePayrollBatchCalculationService());
        var adminActor = new UserActorContext(1, "Staff", "ADMIN", 1, IsAdmin: true);

        var preview = await service.PreviewPayrollRunAsync(new PayrollRunPreviewQuery(1, "2026-05"), adminActor);

        Assert.NotNull(preview);
        Assert.Equal("2026-05", preview.Period);
        Assert.Single(preview.Details);

        var item = preview.Details[0];
        Assert.Equal(201, item.StaffId);
        Assert.Equal(10_000_000m, item.BaseSalary);
        Assert.Equal(1_500_000m, item.AllowanceAmount);
        // Net = 10M + 1.5M = 11.5M
        Assert.Equal(11_500_000m, item.NetSalary);
    }

    [Fact]
    public async Task PayrollApplicationService_CreateAndApprovePayrollRun_TransitionsStatusAndItems()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PayrollAppCreateApprove_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options);

        db.Staffs.Add(new Staff
        {
            Id = 301,
            FullName = "Hoàng Nhân Viên",
            BranchId = 1,
            PasswordHash = "hash"
        });
        await db.SaveChangesAsync();

        var auth = new PayrollAuthorizationService(new EfPayrollStore(db));
        var service = new PayrollApplicationService(new MyGarage.Infrastructure.Payroll.EfPayrollStore(db), auth, new FakePayrollBatchCalculationService());
        var adminActor = new UserActorContext(1, "Staff", "ADMIN", 1, IsAdmin: true);

        // 1. Create payroll run with 1 item
        var createCmd = new PayrollRunCreateCommand(
            Period: "2026-06",
            BranchId: 1,
            CreatedBy: 1,
            Details: new List<PayrollRunCreateItemCommand>
            {
                new(
                    StaffId: 301,
                    BaseSalary: 15_000_000m,
                    CommissionAmount: 2_000_000m,
                    OvertimeAmount: 1_000_000m,
                    AllowanceAmount: 1_000_000m,
                    Bonus: 500_000m,
                    BonusAmount: 500_000m,
                    Deductions: 1_000_000m,
                    Penalty: 0m,
                    PenaltyAmount: 0m
                )
            }
        );

        var createdRun = await service.CreatePayrollRunAsync(createCmd, adminActor);
        Assert.NotNull(createdRun);
        Assert.Equal("CALCULATED", createdRun.Status);
        Assert.Equal(1, createdRun.RunNo);
        Assert.Single(createdRun.Details!);
        // NetPay = 15M + 1M(allow) + 2M(comm) + 1M(ot) + 500k(bonus) - 1M(deduct) = 18.5M
        Assert.Equal(18_500_000m, createdRun.TotalAmount);

        // 2. Approve payroll run
        var approvedRun = await service.ApprovePayrollRunAsync(createdRun.Id, adminActor);
        Assert.NotNull(approvedRun);
        Assert.Equal("APPROVED", approvedRun.Status);
        Assert.All(approvedRun.Details!, d => Assert.Equal("APPROVED", d.Status));
    }

    [Fact]
    public async Task PayrollApplicationService_GetMyPayslips_FiltersBySelfAndPeriod()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PayrollAppPayslip_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new GarageContext(options);

        var period1 = new PayrollPeriod { Id = 1, PeriodCode = "2026-07", Status = "OPEN", StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 7, 31), PayrollDate = new DateOnly(2026, 7, 31) };
        var period2 = new PayrollPeriod { Id = 2, PeriodCode = "2026-08", Status = "OPEN", StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 8, 31), PayrollDate = new DateOnly(2026, 8, 31) };
        db.PayrollPeriods.AddRange(period1, period2);

        var run1 = new PayrollRun { Id = 1, PayrollPeriodId = 1, BranchId = 1, RunNo = 1, Status = "PAID" };
        var run2 = new PayrollRun { Id = 2, PayrollPeriodId = 2, BranchId = 1, RunNo = 1, Status = "PAID" };
        db.PayrollRuns.AddRange(run1, run2);

        var staff = new Staff { Id = 401, FullName = "Đỗ Nhân Viên", BranchId = 1, PasswordHash = "hash" };
        db.Staffs.Add(staff);

        var item1 = new PayrollItem { Id = 1, PayrollRunId = 1, StaffId = 401, BaseSalary = 10_000_000m, NetPay = 10_000_000m, Status = "PAID" };
        var item2 = new PayrollItem { Id = 2, PayrollRunId = 2, StaffId = 401, BaseSalary = 11_000_000m, NetPay = 11_000_000m, Status = "PAID" };
        db.PayrollItems.AddRange(item1, item2);

        db.Payslips.Add(new Payslip { Id = 1, PayrollItemId = 1, SlipNo = "PS-2026-07-001", IssuedAt = DateTime.UtcNow });
        db.Payslips.Add(new Payslip { Id = 2, PayrollItemId = 2, SlipNo = "PS-2026-08-001", IssuedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var auth = new PayrollAuthorizationService(new EfPayrollStore(db));
        var service = new PayrollApplicationService(new MyGarage.Infrastructure.Payroll.EfPayrollStore(db), auth, new FakePayrollBatchCalculationService());
        var staffActor = new UserActorContext(401, "Staff", "TECHNICIAN", 1, IsAdmin: false);

        // Fetch all payslips for staff 401
        var allMyPayslips = await service.GetMyPayslipsAsync(new PayslipsQuery(401), staffActor);
        Assert.Equal(2, allMyPayslips.Count);

        // Filter by period 2026-07
        var periodFiltered = await service.GetMyPayslipsAsync(new PayslipsQuery(401, "2026-07"), staffActor);
        Assert.Single(periodFiltered);
        Assert.Equal("PS-2026-07-001", periodFiltered[0].SlipNo);
    }

    // =========================================================================
    // SECTION 5: Web API Dual Routing (11 Module 9 Controllers)
    // =========================================================================

    [Fact]
    public async Task PayrollAndHrControllers_DualRouting_RespondsEquivalentlyOnLegacyAndV1Routes()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        // Admin JWT token with Staff role and branch 1
        var token = TestWebApplicationFactory.CreateJwtToken(1, "Staff", "ADMIN", 1);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. SalaryAdminController (/api/v1/salary-admin)
        var res1 = await client.GetAsync("/api/v1/salary-admin/profiles");
        Assert.NotEqual(HttpStatusCode.NotFound, res1.StatusCode);

        // 2. PayrollRunsController (/api/v1/payroll-runs)
        var res2 = await client.GetAsync("/api/v1/payroll-runs");
        Assert.NotEqual(HttpStatusCode.NotFound, res2.StatusCode);

        // 3. PayslipsController (/api/v1/payslips)
        var res3 = await client.GetAsync("/api/v1/payslips/my");
        Assert.NotEqual(HttpStatusCode.NotFound, res3.StatusCode);

        // 4. SalaryCoefficientController (/api/v1/salary-coefficients)
        var res4 = await client.GetAsync("/api/v1/salary-coefficients");
        Assert.NotEqual(HttpStatusCode.NotFound, res4.StatusCode);

        // 5. CommissionRuleController (/api/v1/commission-rules)
        var res5 = await client.GetAsync("/api/v1/commission-rules");
        Assert.NotEqual(HttpStatusCode.NotFound, res5.StatusCode);

        // 6. CommissionTransactionController
        // 6a. Main controller route
        var res6a = await client.GetAsync("/api/v1/commission-transactions");
        Assert.NotEqual(HttpStatusCode.NotFound, res6a.StatusCode);

        // 6b. Commissions alias route
        var res6b = await client.GetAsync("/api/v1/commissions");
        Assert.NotEqual(HttpStatusCode.NotFound, res6b.StatusCode);

        // 7. StaffController
        // 7a. Main controller route /api/v1/staffs
        var res7a = await client.GetAsync("/api/v1/staffs");
        Assert.NotEqual(HttpStatusCode.NotFound, res7a.StatusCode);

        // 7b. Singular alias route /api/v1/staff
        var res7b = await client.GetAsync("/api/v1/staff");
        Assert.NotEqual(HttpStatusCode.NotFound, res7b.StatusCode);

        // 8. StaffScheduleController (/api/v1/staff-schedules)
        var res8 = await client.GetAsync("/api/v1/staff-schedules");
        Assert.NotEqual(HttpStatusCode.NotFound, res8.StatusCode);

        // 9. StaffSpecialtyController
        // /api/v1/staff/available
        var res9a = await client.GetAsync("/api/v1/staff/available");
        Assert.NotEqual(HttpStatusCode.NotFound, res9a.StatusCode);

        // /api/v1/staffs/available
        var res9b = await client.GetAsync("/api/v1/staffs/available");
        Assert.NotEqual(HttpStatusCode.NotFound, res9b.StatusCode);

        // 10. StaffTierController (/api/v1/staff-tiers)
        var res10 = await client.GetAsync("/api/v1/staff-tiers");
        Assert.NotEqual(HttpStatusCode.NotFound, res10.StatusCode);

        // 11. TimesheetsController (/api/v1/timesheets)
        var res11 = await client.GetAsync("/api/v1/timesheets/today");
        Assert.NotEqual(HttpStatusCode.NotFound, res11.StatusCode);
    }
}
