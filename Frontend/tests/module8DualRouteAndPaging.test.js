import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { buildBoundedPaginationParams, parseVehicleListPage } from "../src/features/cars/api/vehiclePaging.js";
import { requestWithDualRoute as vehicleDualRoute, vehicleAdminApi } from "../src/features/cars/api/vehicleAdminApi.js";
import {
  requestWithDualRoute as customerDualRoute,
  getAllCustomers,
  getCustomerPage,
  getCustomerById,
  createCustomer,
  updateCustomer,
  deleteCustomer,
  getAllCustomerTiers,
  getAllCustomerVehicles,
  createCustomerVehicle,
  updateCustomerVehicle,
  deleteCustomerVehicle,
  getAllVehicles,
  getVehicleById,
  createVehicle,
  updateVehicle,
} from "../src/features/admin/api/customerApi.js";
import {
  requestWithDualRoute as catalogDualRoute,
  getAllVehicleBrands,
  getVehicleBrandPage,
  createVehicleBrand,
  updateVehicleBrand,
  deleteVehicleBrand,
  getAllVehicleModels,
  getVehicleModelPage,
  createVehicleModel,
  updateVehicleModel,
  deleteVehicleModel,
} from "../src/features/admin/api/vehicleCatalogApi.js";
import { servicesApi, requestWithDualRoute as servicesDualRoute } from "../src/features/services/api/servicesApi.js";
import { getServiceCategoryPage, requestWithDualRoute as categoryPagingDualRoute } from "../src/features/services/api/serviceCategoryPagingApi.js";
import {
  getAllServiceCategories,
  getAllStandardServices,
  getStandardServicePage,
  createStandardService,
  updateStandardService,
  deleteStandardService,
} from "../src/features/admin/api/serviceApi.js";

// Helper for capturing calls and responses
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
    return { ok: true, data: [] };
  };
  return { calls, requester };
}

test("buildBoundedPaginationParams enforces boundary limits", () => {
  const normal = buildBoundedPaginationParams({ page: 2, pageSize: 30, branchId: 5 });
  assert.equal(normal.page, 2);
  assert.equal(normal.pageSize, 30);
  assert.equal(normal.branchId, 5);

  const defaults = buildBoundedPaginationParams();
  assert.equal(defaults.page, 1);
  assert.equal(defaults.pageSize, 20);

  const clampedLower = buildBoundedPaginationParams({ page: -5, pageSize: 0 });
  assert.equal(clampedLower.page, 1);
  assert.equal(clampedLower.pageSize, 20);

  const clampedUpper = buildBoundedPaginationParams({ page: 1, pageSize: 5000 }, 20, 200);
  assert.equal(clampedUpper.page, 1);
  assert.equal(clampedUpper.pageSize, 200);
});

test("parseVehicleListPage handles structured, array, and null payloads safely", () => {
  const structured = parseVehicleListPage({ items: [{ id: 1 }], total: 42 });
  assert.equal(structured.items.length, 1);
  assert.equal(structured.total, 42);

  const arrayPayload = parseVehicleListPage([{ id: 1 }, { id: 2 }]);
  assert.equal(arrayPayload.items.length, 2);
  assert.equal(arrayPayload.total, 2);

  const empty = parseVehicleListPage(null);
  assert.deepEqual(empty, { items: [], total: 0 });
});

test("customerApi routes to v1 first and falls back to legacy on 404", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url, callCount) => callCount === 1 && url.startsWith("/api/v1/"),
    handle: (config) => ({ items: [], total: 0 }),
  });

  await getAllCustomers(2, {}, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/customers");
  assert.equal(calls[1].url, "/api/v1/customers");
  assert.equal(calls[0].params.branchId, 2);
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 100);

  calls.length = 0;
  await getCustomerPage({ search: "Nguyen" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/customers/admin-list");
  assert.equal(calls[1].url, "/api/v1/customers/admin-list");
  assert.equal(calls[0].params.search, "Nguyen");
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 20);
});

test("vehicleCatalogApi dual routes brand and model management", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url, callCount) => callCount === 1 && url.startsWith("/api/v1/"),
  });

  await getAllVehicleBrands({}, requester);
  assert.equal(calls[0].url, "/api/v1/vehicle-brands");
  assert.equal(calls[1].url, "/api/v1/vehicle-brands");
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 200);

  calls.length = 0;
  await getVehicleModelPage({ brandId: 3 }, requester);
  assert.equal(calls[0].url, "/api/v1/vehicle-models/admin-list");
  assert.equal(calls[1].url, "/api/v1/vehicle-models/admin-list");
  assert.equal(calls[0].params.brandId, 3);
  assert.equal(calls[0].params.pageSize, 20);
});

test("servicesApi and serviceCategoryPagingApi dual routes catalog endpoints", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url, callCount) => callCount === 1 && url.startsWith("/api/v1/"),
  });

  await servicesApi.getServiceCategories({}, requester);
  assert.equal(calls[0].url, "/api/v1/service-categories");
  assert.equal(calls[1].url, "/api/v1/service-categories");
  assert.equal(calls[0].params.pageSize, 100);

  calls.length = 0;
  await servicesApi.getStandardServicePage({ search: "Bao duong" }, requester);
  assert.equal(calls[0].url, "/api/v1/standard-services/admin-list");
  assert.equal(calls[1].url, "/api/v1/standard-services/admin-list");
  assert.equal(calls[0].params.search, "Bao duong");
  assert.equal(calls[0].params.pageSize, 20);

  calls.length = 0;
  await getServiceCategoryPage({ page: 2 }, requester);
  assert.equal(calls[0].url, "/api/v1/service-categories/admin-list");
  assert.equal(calls[1].url, "/api/v1/service-categories/admin-list");
  assert.equal(calls[0].params.page, 2);
});

test("serviceApi adapter delegates cleanly with dual route support", async () => {
  const { calls, requester } = createMockRequester();

  await getAllServiceCategories({}, requester);
  assert.equal(calls[0].url, "/api/v1/service-categories");

  calls.length = 0;
  await getAllStandardServices(1, {}, requester);
  assert.equal(calls[0].url, "/api/v1/standard-services");
  assert.equal(calls[0].params.branchId, 1);
  assert.equal(calls[0].params.pageSize, 100);
});

test("vehicleAdminApi bounds pagination and takeHistory parameters", async () => {
  const { calls, requester } = createMockRequester();

  await vehicleAdminApi.getList({ page: 3, pageSize: 50 }, requester);
  assert.equal(calls[0].url, "/api/v1/admin/vehicles");
  assert.equal(calls[0].params.page, 3);
  assert.equal(calls[0].params.pageSize, 50);

  calls.length = 0;
  await vehicleAdminApi.getVehicleDetail(12, {}, requester);
  assert.equal(calls[0].url, "/api/v1/admin/vehicles/12");
  assert.equal(calls[0].params.takeHistory, 20);

  calls.length = 0;
  await vehicleAdminApi.getAlerts({}, requester);
  assert.equal(calls[0].url, "/api/v1/admin/vehicles/alerts");
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 50);
});

test("horizon routes mounts VehicleManagementPage under vehicleItems (/admin/vehicles)", () => {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const routesFile = path.resolve(__dirname, "../src/horizon/routes.jsx");
  const content = fs.readFileSync(routesFile, "utf8");

  assert.match(content, /import VehicleManagementPage from ["']@features\/cars\/pages\/VehicleManagementPage["']/);
  assert.match(content, /path:\s*['"]\/vehicles['"]/);
  assert.match(content, /component:\s*<VehicleManagementPage\s*\/>/);
});
