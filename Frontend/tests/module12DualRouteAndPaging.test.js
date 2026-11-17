import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  buildBoundedPaginationParams as buildAuditPaginationParams,
  getAdminChangeLogs,
  getAuditLogs,
  getAuditLogById,
} from "../src/features/admin/api/auditLogApi.js";

import {
  buildBoundedPaginationParams as buildBranchPaginationParams,
  getAllBranches,
  getBranchPage,
  createBranch,
  updateBranch,
  deleteBranch,
} from "../src/features/admin/api/branchApi.js";

import {
  buildBoundedPaginationParams as buildGatePassApprovalPaginationParams,
  getGatePasses,
  getGatePassById,
  createGatePass,
  updateGatePass,
  deleteGatePass,
  getApprovalRequests,
  getApprovalRequestById,
  createApprovalRequest,
  updateApprovalRequest,
} from "../src/features/admin/api/gatePassAndApprovalApi.js";

function createMockRequester(behavior = {}) {
  const calls = [];
  const requester = async (config) => {
    calls.push(config);
    if (behavior.shouldFail404 && behavior.shouldFail404(config.url, calls.length)) {
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
    return { ok: true, items: [], total: 0 };
  };
  return { calls, requester };
}

test("buildBoundedPaginationParams in auditLogApi, branchApi, and gatePassAndApprovalApi enforces boundary limits", () => {
  // auditLogApi defaultPageSize = 50, maxPageSize = 200
  const auditDefault = buildAuditPaginationParams();
  assert.equal(auditDefault.page, 1);
  assert.equal(auditDefault.pageSize, 50);

  const auditClamped = buildAuditPaginationParams({ page: -3, pageSize: 999, action: "UPDATE" });
  assert.equal(auditClamped.page, 1);
  assert.equal(auditClamped.pageSize, 200);
  assert.equal(auditClamped.action, "UPDATE");

  // branchApi & gatePassAndApprovalApi defaultPageSize = 25, maxPageSize = 200
  for (const buildFn of [buildBranchPaginationParams, buildGatePassApprovalPaginationParams]) {
    const defaults = buildFn();
    assert.equal(defaults.page, 1);
    assert.equal(defaults.pageSize, 25);

    const normal = buildFn({ page: 3, pageSize: 40, search: "chi nhánh" });
    assert.equal(normal.page, 3);
    assert.equal(normal.pageSize, 40);
    assert.equal(normal.search, "chi nhánh");

    const clampedLow = buildFn({ page: 0, pageSize: -10 });
    assert.equal(clampedLow.page, 1);
    assert.equal(clampedLow.pageSize, 25);

    const clampedHigh = buildFn({ page: 2, pageSize: 500 });
    assert.equal(clampedHigh.page, 2);
    assert.equal(clampedHigh.pageSize, 200);
  }
});

test("auditLogApi dual routes endpoints to /api/v1/audit-logs/... and falls back on 404 while cleaning empty params", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url, callCount) => callCount === 1 && url.startsWith("/api/v1/"),
    handle: (config) => ({ ok: true, url: config.url }),
  });

  await getAdminChangeLogs(
    { page: 2, pageSize: 500, action: "CREATE", tableName: "", query: null, from: undefined },
    requester
  );
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/audit-logs/admin-changes");
  assert.equal(calls[1].url, "/api/v1/audit-logs/admin-changes");
  assert.equal(calls[0].method, "GET");
  assert.equal(calls[0].params.page, 2);
  assert.equal(calls[0].params.pageSize, 200);
  assert.equal(calls[0].params.action, "CREATE");
  assert.ok(!("tableName" in calls[0].params));
  assert.ok(!("query" in calls[0].params));
  assert.ok(!("from" in calls[0].params));

  calls.length = 0;
  await getAuditLogs({ page: -1, pageSize: 30, tableName: "Branches" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/audit-logs");
  assert.equal(calls[1].url, "/api/v1/audit-logs");
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 30);
  assert.equal(calls[0].params.tableName, "Branches");

  calls.length = 0;
  await getAuditLogById(42, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/audit-logs/42");
  assert.equal(calls[1].url, "/api/v1/audit-logs/42");
  assert.equal(calls[0].method, "GET");
});

test("branchApi dual routes all branch administration endpoints and falls back on 404", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url, callCount) => callCount === 1 && url.startsWith("/api/v1/"),
    handle: (config) => ({ ok: true, url: config.url }),
  });

  await getAllBranches(requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/branches");
  assert.equal(calls[1].url, "/api/v1/branches");
  assert.equal(calls[0].method, "GET");

  calls.length = 0;
  await getBranchPage({ search: "Hà Nội", page: 1, pageSize: 999 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/branches/admin-list");
  assert.equal(calls[1].url, "/api/v1/branches/admin-list");
  assert.equal(calls[0].params.search, "Hà Nội");
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 200);

  calls.length = 0;
  await createBranch({ name: "Chi nhánh Cầu Giấy", maxWorkSlots: 20 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/branches");
  assert.equal(calls[1].url, "/api/v1/branches");
  assert.equal(calls[0].method, "POST");
  assert.equal(calls[0].data.name, "Chi nhánh Cầu Giấy");

  calls.length = 0;
  await updateBranch(5, { name: "Chi nhánh Mỹ Đình" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/branches/5");
  assert.equal(calls[1].url, "/api/v1/branches/5");
  assert.equal(calls[0].method, "PUT");

  calls.length = 0;
  const deleted = await deleteBranch(5, requester);
  assert.equal(deleted, true);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/branches/5");
  assert.equal(calls[1].url, "/api/v1/branches/5");
  assert.equal(calls[0].method, "DELETE");
});

test("gatePassAndApprovalApi dual routes all gate pass and approval request endpoints and falls back on 404", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url, callCount) => callCount === 1 && url.startsWith("/api/v1/"),
    handle: (config) => ({ ok: true, url: config.url }),
  });

  // Gate Passes
  await getGatePasses({ branchId: 1, page: 0, pageSize: 500 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/gate-passes");
  assert.equal(calls[1].url, "/api/v1/gate-passes");
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 200);

  calls.length = 0;
  await getGatePassById(101, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/gate-passes/101");
  assert.equal(calls[1].url, "/api/v1/gate-passes/101");
  assert.equal(calls[0].method, "GET");

  calls.length = 0;
  await createGatePass({ serviceOrderId: 88, licensePlate: "30A-123.45" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/gate-passes");
  assert.equal(calls[1].url, "/api/v1/gate-passes");
  assert.equal(calls[0].method, "POST");

  calls.length = 0;
  await updateGatePass(101, { status: "APPROVED" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/gate-passes/101");
  assert.equal(calls[1].url, "/api/v1/gate-passes/101");
  assert.equal(calls[0].method, "PUT");

  calls.length = 0;
  await deleteGatePass(101, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/gate-passes/101");
  assert.equal(calls[1].url, "/api/v1/gate-passes/101");
  assert.equal(calls[0].method, "DELETE");

  // Approval Requests
  calls.length = 0;
  await getApprovalRequests({ status: "PENDING", page: 2, pageSize: 50 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/approval-requests");
  assert.equal(calls[1].url, "/api/v1/approval-requests");
  assert.equal(calls[0].params.page, 2);
  assert.equal(calls[0].params.pageSize, 50);

  calls.length = 0;
  await getApprovalRequestById(77, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/approval-requests/77");
  assert.equal(calls[1].url, "/api/v1/approval-requests/77");
  assert.equal(calls[0].method, "GET");

  calls.length = 0;
  await createApprovalRequest({ requestType: "DISCOUNT", targetId: 15 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/approval-requests");
  assert.equal(calls[1].url, "/api/v1/approval-requests");
  assert.equal(calls[0].method, "POST");

  calls.length = 0;
  await updateApprovalRequest(77, { status: "APPROVED" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/approval-requests/77");
  assert.equal(calls[1].url, "/api/v1/approval-requests/77");
  assert.equal(calls[0].method, "PUT");
});

test("Module 12 dual routing propagates 500 server error without fallback", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail500: (url) => url.startsWith("/api/v1/"),
  });

  await assert.rejects(
    async () => {
      await getAdminChangeLogs({ page: 1 }, requester);
    },
    (err) => err.status === 500
  );
  assert.equal(calls.length, 1);

  calls.length = 0;
  await assert.rejects(
    async () => {
      await getBranchPage({ page: 1 }, requester);
    },
    (err) => err.status === 500
  );
  assert.equal(calls.length, 1);

  calls.length = 0;
  await assert.rejects(
    async () => {
      await getGatePasses({ page: 1 }, requester);
    },
    (err) => err.status === 500
  );
  assert.equal(calls.length, 1);

  calls.length = 0;
  await assert.rejects(
    async () => {
      await getApprovalRequests({ page: 1 }, requester);
    },
    (err) => err.status === 500
  );
  assert.equal(calls.length, 1);
});

test("UTF-8 strictness & Vietnamese diacritics on Module 12 files", () => {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const filesToCheck = [
    "../src/features/admin/api/auditLogApi.js",
    "../src/features/admin/api/branchApi.js",
    "../src/features/admin/api/gatePassAndApprovalApi.js",
    "../src/features/admin/pages/AdminChangeLogPage.jsx",
    "../src/features/admin/pages/AdminPage.jsx",
    "../src/features/auth/pages/AuthPage.jsx",
    "../src/features/auth/recovery/ResetPasswordPage.jsx",
    "../src/shared/core/components/ErrorBoundary.jsx",
  ];

  for (const relPath of filesToCheck) {
    const fullPath = path.resolve(__dirname, relPath);
    const content = fs.readFileSync(fullPath, "utf8");

    assert.ok(!content.includes("\uFFFD"), `File ${relPath} contains \\uFFFD replacement char`);
    assert.ok(!content.includes("ðŸ"), `File ${relPath} contains mojibake ðŸ`);
    assert.ok(!content.includes("\u00c3\u0192"), `File ${relPath} contains mojibake \u00c3\u0192`);
  }

  const changeLogPath = path.resolve(__dirname, "../src/features/admin/pages/AdminChangeLogPage.jsx");
  const changeLogContent = fs.readFileSync(changeLogPath, "utf8");
  assert.ok(
    changeLogContent.includes('SalaryRequests: "Yêu cầu lương"'),
    "AdminChangeLogPage.jsx must contain 'SalaryRequests: \"Yêu cầu lương\"'"
  );
});

test("Module 12 routes and components are mounted in App.jsx and Horizon routes", () => {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const appFile = path.resolve(__dirname, "../src/app/App.jsx");
  const appContent = fs.readFileSync(appFile, "utf8");

  assert.ok(appContent.includes("<ErrorBoundary>"), "App.jsx wraps application in ErrorBoundary");
  assert.ok(appContent.includes('path="/login"'), "App.jsx mounts /login");
  assert.ok(appContent.includes("<AuthPage />"), "App.jsx mounts AuthPage");
  assert.ok(appContent.includes('path="/reset-password"'), "App.jsx mounts /reset-password");
  assert.ok(appContent.includes("<ResetPasswordPage />"), "App.jsx mounts ResetPasswordPage");
  assert.ok(appContent.includes('path="/admin/*"'), "App.jsx mounts /admin/*");
  assert.ok(appContent.includes('path="/admin-legacy"'), "App.jsx mounts /admin-legacy");
  assert.ok(appContent.includes("<AdminPage />"), "App.jsx mounts AdminPage");
});
