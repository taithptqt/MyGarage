import test from "node:test";
import assert from "node:assert/strict";
import { buildBoundedPaginationParams, parseVehicleListPage } from "./vehiclePaging.js";
import { requestWithDualRoute, vehicleAdminApi } from "./vehicleAdminApi.js";
import { getAllCustomers, getAllVehicles } from "../../admin/api/customerApi.js";
import { getAllVehicleBrands, getAllVehicleModels } from "../../admin/api/vehicleCatalogApi.js";
import { servicesApi } from "../../services/api/servicesApi.js";

test("buildBoundedPaginationParams sets bounded defaults and preserves filters", () => {
  const result = buildBoundedPaginationParams({ brandId: 4, status: "REPAIRING" }, 20, 200);
  assert.equal(result.page, 1);
  assert.equal(result.pageSize, 20);
  assert.equal(result.brandId, 4);
  assert.equal(result.status, "REPAIRING");
});

test("buildBoundedPaginationParams clamps oversized pageSize to maxPageSize", () => {
  const result = buildBoundedPaginationParams({ page: 3, pageSize: 9999 }, 20, 200);
  assert.equal(result.page, 3);
  assert.equal(result.pageSize, 200);
});

test("parseVehicleListPage normalizes valid and empty payloads", () => {
  const sample = parseVehicleListPage({ items: [{ id: 10, vehicleCode: "VH-10" }], total: 1 });
  assert.deepEqual(sample, { items: [{ id: 10, vehicleCode: "VH-10" }], total: 1 });

  const empty = parseVehicleListPage(null);
  assert.deepEqual(empty, { items: [], total: 0 });
});

test("requestWithDualRoute calls v1 route first and returns result on success", async () => {
  const calls = [];
  const fakeRequester = async (config) => {
    calls.push(config);
    return { data: [{ id: 1 }] };
  };

  const res = await requestWithDualRoute("/api/v1/vehicles", "/api/v1/legacy/vehicles", { method: "GET" }, fakeRequester);
  assert.equal(calls.length, 1);
  assert.equal(calls[0].url, "/api/v1/vehicles");
  assert.deepEqual(res, { data: [{ id: 1 }] });
});

test("requestWithDualRoute falls back to legacy route when v1 returns 404", async () => {
  const calls = [];
  const fakeRequester = async (config) => {
    calls.push(config);
    if (config.url === "/api/v1/vehicles") {
      const err = new Error("Not Found");
      err.status = 404;
      throw err;
    }
    return { data: [{ id: 2 }] };
  };

  const res = await requestWithDualRoute("/api/v1/vehicles", "/api/v1/legacy/vehicles", { method: "GET" }, fakeRequester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/vehicles");
  assert.equal(calls[1].url, "/api/v1/legacy/vehicles");
  assert.deepEqual(res, { data: [{ id: 2 }] });
});

test("requestWithDualRoute does not fallback on non-404 errors (e.g. 500)", async () => {
  const calls = [];
  const fakeRequester = async (config) => {
    calls.push(config);
    const err = new Error("Internal Server Error");
    err.status = 500;
    throw err;
  };

  await assert.rejects(
    () => requestWithDualRoute("/api/v1/vehicles", "/api/v1/legacy/vehicles", { method: "GET" }, fakeRequester),
    /Internal Server Error/
  );
  assert.equal(calls.length, 1);
});

test("vehicleAdminApi.getList passes explicit page and pageSize parameters", async () => {
  let passedConfig = null;
  const fakeRequester = async (config) => {
    passedConfig = config;
    return { items: [], total: 0 };
  };

  await vehicleAdminApi.getList({}, fakeRequester);
  assert.ok(passedConfig);
  assert.equal(passedConfig.params.page, 1);
  assert.equal(passedConfig.params.pageSize, 20);
});

test("getAllCustomers and getAllVehicles pass explicit bounded pagination to avoid collectPages loop", async () => {
  const recorded = [];
  const fakeRequester = async (config) => {
    recorded.push(config);
    return [];
  };

  await getAllCustomers(1, {}, fakeRequester);
  await getAllVehicles({}, fakeRequester);
  await getAllVehicleBrands({}, fakeRequester);
  await getAllVehicleModels({}, fakeRequester);
  await servicesApi.getStandardServices(1, {}, fakeRequester);

  assert.equal(recorded.length, 5);
  for (const call of recorded) {
    assert.ok(call.params, `Expected params in ${call.url}`);
    assert.ok(call.params.page >= 1, `Expected page in ${call.url}`);
    assert.ok(call.params.pageSize >= 1, `Expected pageSize in ${call.url}`);
  }
});
