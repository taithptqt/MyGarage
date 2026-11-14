import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  buildBoundedPaginationParams,
  getPayrollRuns,
  getPayrollPreview,
  createPayrollRun,
  approvePayrollRun,
  requestWithDualRoute as payrollDualRoute,
} from "../src/features/hr/api/payrollApi.js";
import {
  getCommissions,
  requestWithDualRoute as commissionDualRoute,
} from "../src/features/hr/api/commissionApi.js";
import {
  getMyPayslips,
  requestWithDualRoute as payslipDualRoute,
} from "../src/features/payslip/api/payslipApi.js";
import {
  getSalaryProfiles,
  createSalaryProfile,
  updateSalaryProfile,
  deleteSalaryProfile,
  getInitialSalaryRequests,
  approveInitialSalaryRequest,
  rejectInitialSalaryRequest,
  requestWithDualRoute as salaryAdminDualRoute,
  requestSafeWithDualRoute as salaryAdminSafeDualRoute,
} from "../src/features/admin/api/salaryAdminApi.js";
import {
  getAllSalaryCoefficients,
  createSalaryCoefficient,
  updateSalaryCoefficient,
  deleteSalaryCoefficient,
  requestWithDualRoute as salaryCoeffDualRoute,
} from "../src/features/admin/api/salaryCoefficientApi.js";

// Helper for capturing calls and creating mock responses
function createMockRequester(behavior = {}) {
  const calls = [];
  const requester = async (config) => {
    calls.push(config);
    if (behavior.shouldFail404 && behavior.shouldFail404(config.url)) {
      const err = new Error(`Not Found: ${config.url}`);
      err.status = 404;
      err.response = { status: 404 };
      throw err;
    }
    if (behavior.shouldFail500 && behavior.shouldFail500(config.url)) {
      const err = new Error(`Server Error: ${config.url}`);
      err.status = 500;
      err.response = { status: 500 };
      throw err;
    }
    if (typeof behavior.handle === "function") {
      return behavior.handle(config);
    }
    return { ok: true, data: [] };
  };
  return { calls, requester };
}

test("buildBoundedPaginationParams enforces boundary limits and clamps defaults", () => {
  const normal = buildBoundedPaginationParams({ page: 2, pageSize: 25, branchId: 3 });
  assert.equal(normal.page, 2);
  assert.equal(normal.pageSize, 25);
  assert.equal(normal.branchId, 3);

  const defaults = buildBoundedPaginationParams();
  assert.equal(defaults.page, 1);
  assert.equal(defaults.pageSize, 10);

  const clampedLower = buildBoundedPaginationParams({ page: -1, pageSize: 0 });
  assert.equal(clampedLower.page, 1);
  assert.equal(clampedLower.pageSize, 10);

  const clampedUpper = buildBoundedPaginationParams({ page: 1, pageSize: 500 }, 10, 100);
  assert.equal(clampedUpper.page, 1);
  assert.equal(clampedUpper.pageSize, 100);
});

test("payrollApi dual routes /api/v1/payroll-runs and falls back to /api/payroll-runs on 404", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url) => url.startsWith("/api/v1/"),
    handle: () => ({ items: [{ id: 1, period: "2026-11" }], total: 1 }),
  });

  const res = await getPayrollRuns({ branchId: 2, page: 1, pageSize: 10 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/payroll-runs");
  assert.equal(calls[1].url, "/api/payroll-runs");
  assert.equal(calls[0].params.branchId, 2);
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 10);
  assert.equal(res.items.length, 1);

  calls.length = 0;
  await getPayrollPreview({ branchId: 2, period: "2026-11" }, requester);
  assert.equal(calls[0].url, "/api/v1/payroll-runs/preview");
  assert.equal(calls[1].url, "/api/payroll-runs/preview");

  calls.length = 0;
  await createPayrollRun({ period: "2026-11", branchId: 2 }, requester);
  assert.equal(calls[0].url, "/api/v1/payroll-runs");
  assert.equal(calls[1].url, "/api/payroll-runs");

  calls.length = 0;
  await approvePayrollRun(5, { approvedBy: 1 }, requester);
  assert.equal(calls[0].url, "/api/v1/payroll-runs/5/approve");
  assert.equal(calls[1].url, "/api/payroll-runs/5/approve");
});

test("payrollApi propagates 500 server error without fallback", async () => {
  const { requester } = createMockRequester({
    shouldFail500: (url) => url.startsWith("/api/v1/"),
  });

  await assert.rejects(
    async () => {
      await getPayrollRuns({ branchId: 1 }, requester);
    },
    (err) => err.status === 500
  );
});

test("commissionApi and payslipApi dual route and fall back cleanly", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url) => url.startsWith("/api/v1/"),
    handle: () => ({ items: [] }),
  });

  await getCommissions({ branchId: 4, period: "2026-11" }, requester);
  assert.equal(calls[0].url, "/api/v1/commissions");
  assert.equal(calls[1].url, "/api/commissions");
  assert.equal(calls[0].params.branchId, 4);
  assert.equal(calls[0].params.period, "2026-11");

  calls.length = 0;
  await getMyPayslips({}, requester);
  assert.equal(calls[0].url, "/api/v1/payslips/my");
  assert.equal(calls[1].url, "/api/payslips/my");
});

test("salaryAdminApi dual routes profiles and request workflows", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url) => url.startsWith("/api/v1/"),
    handle: () => [{ id: 10 }],
  });

  await getSalaryProfiles(3, {}, requester);
  assert.equal(calls[0].url, "/api/v1/salary-admin/profiles");
  assert.equal(calls[1].url, "/api/salary-admin/profiles");
  assert.equal(calls[0].params.branchId, 3);

  calls.length = 0;
  await createSalaryProfile({ staffId: 1, baseSalary: 10000000 }, requester);
  assert.equal(calls[0].url, "/api/v1/salary-admin/profiles");
  assert.equal(calls[1].url, "/api/salary-admin/profiles");

  calls.length = 0;
  await updateSalaryProfile(10, { baseSalary: 12000000 }, requester);
  assert.equal(calls[0].url, "/api/v1/salary-admin/profiles/10");
  assert.equal(calls[1].url, "/api/salary-admin/profiles/10");

  calls.length = 0;
  await deleteSalaryProfile(10, requester);
  assert.equal(calls[0].url, "/api/v1/salary-admin/profiles/10");
  assert.equal(calls[1].url, "/api/salary-admin/profiles/10");

  calls.length = 0;
  await getInitialSalaryRequests({ status: "PENDING" }, requester);
  assert.equal(calls[0].url, "/api/v1/salary-admin/requests");
  assert.equal(calls[1].url, "/api/salary-admin/requests");

  calls.length = 0;
  await approveInitialSalaryRequest(7, { note: "Approved" }, requester);
  assert.equal(calls[0].url, "/api/v1/salary-admin/requests/7/approve");
  assert.equal(calls[1].url, "/api/salary-admin/requests/7/approve");

  calls.length = 0;
  await rejectInitialSalaryRequest(7, { reason: "Too high" }, requester);
  assert.equal(calls[0].url, "/api/v1/salary-admin/requests/7/reject");
  assert.equal(calls[1].url, "/api/salary-admin/requests/7/reject");
});

test("salaryCoefficientApi dual routes CRUD endpoints", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url) => url.startsWith("/api/v1/"),
  });

  await getAllSalaryCoefficients({}, requester);
  assert.equal(calls[0].url, "/api/v1/salary-coefficients");
  assert.equal(calls[1].url, "/api/salary-coefficients");

  calls.length = 0;
  await createSalaryCoefficient({ title: "Senior" }, requester);
  assert.equal(calls[0].url, "/api/v1/salary-coefficients");
  assert.equal(calls[1].url, "/api/salary-coefficients");

  calls.length = 0;
  await updateSalaryCoefficient(4, { title: "Lead" }, requester);
  assert.equal(calls[0].url, "/api/v1/salary-coefficients/4");
  assert.equal(calls[1].url, "/api/salary-coefficients/4");

  calls.length = 0;
  await deleteSalaryCoefficient(4, requester);
  assert.equal(calls[0].url, "/api/v1/salary-coefficients/4");
  assert.equal(calls[1].url, "/api/salary-coefficients/4");
});

test("UTF-8 strictness: Module 9 files contain 0 corrupted chars, 0 mojibake, valid Vietnamese", () => {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const filesToCheck = [
    "../src/features/hr/pages/PayrollRunPage.jsx",
    "../src/features/hr/pages/CommissionBreakdownPage.jsx",
    "../src/features/payslip/pages/MyPayslipPage.jsx",
    "../src/features/admin/pages/AdminSalaryPage.jsx",
    "../src/horizon/views/admin/accounting/pages/PayrollPage.jsx",
    "../src/horizon/views/admin/accounting/pages/PayslipHistoryPage.jsx",
  ];

  for (const relPath of filesToCheck) {
    const fullPath = path.resolve(__dirname, relPath);
    const content = fs.readFileSync(fullPath, "utf8");

    // 0 replacement characters
    assert.ok(!content.includes("\uFFFD"), `File ${relPath} contains \\uFFFD replacement char`);

    // 0 mojibake patterns
    assert.ok(!content.includes("ðŸ‘¤"), `File ${relPath} contains mojibake ðŸ‘¤`);
    assert.ok(!content.includes("Kh?ng"), `File ${relPath} contains corrupted Kh?ng`);
    assert.ok(!content.includes("b?n nh?p"), `File ${relPath} contains corrupted b?n nh?p`);
    assert.ok(!content.includes("C?p nh?t"), `File ${relPath} contains corrupted C?p nh?t`);
    assert.ok(!content.includes(' + " ?"'), `File ${relPath} contains corrupted currency + " ?"`);
  }

  // Check specific restored phrases
  const payrollRunContent = fs.readFileSync(path.resolve(__dirname, "../src/features/hr/pages/PayrollRunPage.jsx"), "utf8");
  assert.ok(payrollRunContent.includes("Không thể tải danh sách bảng lương."));
  assert.ok(payrollRunContent.includes("Lỗi tạo bản nháp lương."));
  assert.ok(payrollRunContent.includes("📋 Bảng lương"));
  assert.ok(payrollRunContent.includes("← Quay lại"));
  assert.ok(payrollRunContent.includes("Tổng Quỹ Lương:"));
  assert.ok(payrollRunContent.includes("✓ Duyệt Bảng Lương"));
  assert.ok(payrollRunContent.includes("💰 Quản lý lương thưởng (Payroll)"));

  const commContent = fs.readFileSync(path.resolve(__dirname, "../src/features/hr/pages/CommissionBreakdownPage.jsx"), "utf8");
  assert.ok(commContent.includes("👤 {group.staffName}"));

  const payslipContent = fs.readFileSync(path.resolve(__dirname, "../src/features/payslip/pages/MyPayslipPage.jsx"), "utf8");
  assert.ok(payslipContent.includes("Không thể tải danh sách phiếu lương."));

  const adminSalaryContent = fs.readFileSync(path.resolve(__dirname, "../src/features/admin/pages/AdminSalaryPage.jsx"), "utf8");
  assert.ok(adminSalaryContent.includes("Bảng lương nhân viên"));
  assert.ok(adminSalaryContent.includes("Cập nhật bảng lương"));

  const accountingPayrollContent = fs.readFileSync(path.resolve(__dirname, "../src/horizon/views/admin/accounting/pages/PayrollPage.jsx"), "utf8");
  assert.ok(accountingPayrollContent.includes("Kết quả duyệt lương khởi tạo"));
  assert.ok(accountingPayrollContent.includes("Yêu cầu lương bị Admin từ chối"));
});

test("Module 9 route definitions are mounted in application router", () => {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const appFile = path.resolve(__dirname, "../src/app/App.jsx");
  const appContent = fs.readFileSync(appFile, "utf8");

  assert.ok(appContent.includes('path="hr/payroll-run"'));
  assert.ok(appContent.includes('path="hr/commissions"'));
  assert.ok(appContent.includes('path="payslip"'));

  const horizonRoutesFile = path.resolve(__dirname, "../src/horizon/routes.jsx");
  const horizonContent = fs.readFileSync(horizonRoutesFile, "utf8");
  assert.ok(horizonContent.includes("path: '/salary'"));

  const accountingRoutesFile = path.resolve(__dirname, "../src/horizon/accountingRoutes.jsx");
  const accContent = fs.readFileSync(accountingRoutesFile, "utf8");
  assert.ok(accContent.includes('path: "/payroll"'));
  assert.ok(accContent.includes('path: "/payslip-history"'));
});
