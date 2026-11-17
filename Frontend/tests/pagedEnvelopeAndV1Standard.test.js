import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  buildBoundedPaginationParams,
  normalizePagedEnvelope,
  requestWithDualRoute,
} from "../src/app/pagedEnvelope.js";

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
    return { items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 };
  };
  return { calls, requester };
}

test("normalizePagedEnvelope handles standard JSON Paged Envelope accurately", () => {
  const payload = {
    items: [{ id: 1 }, { id: 2 }],
    totalCount: 52,
    page: 2,
    pageSize: 25,
    totalPages: 3,
  };

  const result = normalizePagedEnvelope(payload);
  assert.deepEqual(result, {
    items: [{ id: 1 }, { id: 2 }],
    totalCount: 52,
    total: 52,
    page: 2,
    pageSize: 25,
    totalPages: 3,
    hasPreviousPage: true,
    hasNextPage: true,
  });
});

test("normalizePagedEnvelope handles envelopes using total or data fields", () => {
  const fromTotal = normalizePagedEnvelope({
    items: [{ id: 10 }],
    total: 15,
    page: 1,
    pageSize: 10,
  });
  assert.equal(fromTotal.items.length, 1);
  assert.equal(fromTotal.totalCount, 15);
  assert.equal(fromTotal.total, 15);
  assert.equal(fromTotal.page, 1);
  assert.equal(fromTotal.pageSize, 10);
  assert.equal(fromTotal.totalPages, 2);
  assert.equal(fromTotal.hasPreviousPage, false);
  assert.equal(fromTotal.hasNextPage, true);

  const fromData = normalizePagedEnvelope({
    data: [{ id: 20 }, { id: 21 }],
    totalCount: 2,
    page: 1,
    pageSize: 20,
  });
  assert.equal(fromData.items.length, 2);
  assert.equal(fromData.totalCount, 2);
  assert.equal(fromData.total, 2);
  assert.equal(fromData.totalPages, 1);
  assert.equal(fromData.hasPreviousPage, false);
  assert.equal(fromData.hasNextPage, false);
});

test("normalizePagedEnvelope handles OData v4 envelope", () => {
  const odataPayload = {
    "@odata.count": 42,
    value: [{ id: 101 }, { id: 102 }],
  };

  const result = normalizePagedEnvelope(odataPayload, 1, 20);
  assert.deepEqual(result.items, [{ id: 101 }, { id: 102 }]);
  assert.equal(result.totalCount, 42);
  assert.equal(result.total, 42);
  assert.equal(result.page, 1);
  assert.equal(result.pageSize, 20);
  assert.equal(result.totalPages, 3);
  assert.equal(result.hasPreviousPage, false);
  assert.equal(result.hasNextPage, true);
});

test("normalizePagedEnvelope handles legacy bare array and null/empty inputs safely", () => {
  const bareArray = [{ id: 1 }, { id: 2 }, { id: 3 }];
  const arrayResult = normalizePagedEnvelope(bareArray, 1, 25);
  assert.equal(arrayResult.items.length, 3);
  assert.equal(arrayResult.totalCount, 3);
  assert.equal(arrayResult.total, 3);
  assert.equal(arrayResult.page, 1);
  assert.equal(arrayResult.pageSize, 25);
  assert.equal(arrayResult.totalPages, 1);
  assert.equal(arrayResult.hasPreviousPage, false);
  assert.equal(arrayResult.hasNextPage, false);

  for (const emptyInput of [null, undefined, "", 123, {}]) {
    const emptyResult = normalizePagedEnvelope(emptyInput, 1, 25);
    assert.deepEqual(emptyResult, {
      items: [],
      totalCount: 0,
      total: 0,
      page: 1,
      pageSize: 25,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
    });
  }
});

test("buildBoundedPaginationParams clamps boundaries and strips empty/null/undefined filters", () => {
  const defaults = buildBoundedPaginationParams();
  assert.deepEqual(defaults, { page: 1, pageSize: 25 });

  const clamped = buildBoundedPaginationParams(
    {
      page: -5,
      pageSize: 999,
      search: "Bảo dưỡng định kỳ",
      branchId: 2,
      emptyString: "",
      nullValue: null,
      undefinedValue: undefined,
      zeroValue: 0,
      falseValue: false,
    },
    25,
    200
  );

  assert.deepEqual(clamped, {
    search: "Bảo dưỡng định kỳ",
    branchId: 2,
    zeroValue: 0,
    falseValue: false,
    page: 1,
    pageSize: 200,
  });

  const lowPageSize = buildBoundedPaginationParams({ page: 3, pageSize: 0 }, 20, 100);
  assert.equal(lowPageSize.page, 3);
  assert.equal(lowPageSize.pageSize, 20);
});

test("requestWithDualRoute prioritizes /api/v1/..., falls back to /api/... on 404, and propagates 500", async () => {
  // 1. Success on /api/v1/... without calling legacy
  const v1Success = createMockRequester({
    handle: (config) => ({ ok: true, url: config.url }),
  });
  const res1 = await requestWithDualRoute(
    "/api/v1/service-orders/page",
    "/api/v1/legacy/service-orders/page",
    { method: "GET", params: { page: 1, pageSize: 25 } },
    v1Success.requester
  );
  assert.equal(v1Success.calls.length, 1);
  assert.equal(v1Success.calls[0].url, "/api/v1/service-orders/page");
  assert.equal(res1.url, "/api/v1/service-orders/page");

  // 2. Fallback to secondary route when primary /api/v1/... returns 404
  const fallback404 = createMockRequester({
    shouldFail404: (url) => url === "/api/v1/service-orders/page",
    handle: (config) => ({ ok: true, url: config.url }),
  });
  const res2 = await requestWithDualRoute(
    "/api/v1/service-orders/page",
    "/api/v1/legacy/service-orders/page",
    { method: "GET", params: { page: 1, pageSize: 25 } },
    fallback404.requester
  );
  assert.equal(fallback404.calls.length, 2);
  assert.equal(fallback404.calls[0].url, "/api/v1/service-orders/page");
  assert.equal(fallback404.calls[1].url, "/api/v1/legacy/service-orders/page");
  assert.equal(res2.url, "/api/v1/legacy/service-orders/page");

  // 3. Do NOT fallback on 500 Server Error
  const error500 = createMockRequester({
    shouldFail500: (url) => url.startsWith("/api/v1/"),
  });
  await assert.rejects(
    async () => {
      await requestWithDualRoute(
        "/api/v1/service-orders/page",
        "/api/v1/legacy/service-orders/page",
        { method: "GET" },
        error500.requester
      );
    },
    (err) => err.status === 500
  );
  assert.equal(error500.calls.length, 1);
});

test("UTF-8 strictness verification on pagedEnvelope module and core API files", () => {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const filesToVerify = [
    "../src/app/pagedEnvelope.js",
    "../src/app/apiClient.js",
    "../src/app/collectPages.js",
  ];

  const mojibakeEmojiPrefix = "\u00F0\u0178";
  const mojibakeLatinPrefix = "\u00C3\u0192";

  for (const relPath of filesToVerify) {
    const fullPath = path.resolve(__dirname, relPath);
    const content = fs.readFileSync(fullPath, "utf8");
    assert.ok(!content.includes("\uFFFD"), `File ${relPath} contains \\uFFFD replacement character`);
    assert.ok(!content.includes(mojibakeEmojiPrefix), `File ${relPath} contains mojibake emoji sequence`);
    assert.ok(!content.includes(mojibakeLatinPrefix), `File ${relPath} contains mojibake Latin sequence`);
  }

  const selfContent = fs.readFileSync(fileURLToPath(import.meta.url), "utf8");
  assert.ok(!selfContent.includes("\uFFFD"), "Test file contains \\uFFFD replacement character");
  assert.ok(!selfContent.includes(mojibakeEmojiPrefix), "Test file contains mojibake emoji sequence");
  assert.ok(!selfContent.includes(mojibakeLatinPrefix), "Test file contains mojibake Latin sequence");
});
