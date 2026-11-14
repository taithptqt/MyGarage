import { buildBoundedPaginationParams } from "./vehiclePaging.js";

async function getRequester(requester) {
  if (typeof requester === "function") return requester;
  const client = await import("@app/apiClient");
  return client.request;
}

/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route if 404 is encountered.
 */
export async function requestWithDualRoute(v1Url, legacyUrl, options = {}, requester) {
  const send = await getRequester(requester);
  try {
    return await send({ ...options, url: v1Url });
  } catch (err) {
    if ((err?.status === 404 || err?.response?.status === 404) && legacyUrl) {
      return await send({ ...options, url: legacyUrl });
    }
    throw err;
  }
}

export const vehicleAdminApi = {
  getLookups: (params = {}, requester) =>
    requestWithDualRoute("/api/v1/vehicle-admin/lookups", "/api/vehicle-admin/lookups", {
      method: "GET",
      params,
    }, requester),

  getList: (params = {}, requester) => {
    const boundedParams = buildBoundedPaginationParams(params, 20, 200);
    return requestWithDualRoute("/api/v1/vehicle-admin/list", "/api/vehicle-admin/list", {
      method: "GET",
      params: boundedParams,
    }, requester);
  },

  getSummary: (params = {}, requester) =>
    requestWithDualRoute("/api/v1/vehicle-admin/summary", "/api/vehicle-admin/summary", {
      method: "GET",
      params,
    }, requester),

  getAlerts: (params = {}, requester) => {
    const boundedParams = buildBoundedPaginationParams(params, 50, 200);
    return requestWithDualRoute("/api/v1/vehicle-admin/alerts", "/api/vehicle-admin/alerts", {
      method: "GET",
      params: boundedParams,
    }, requester);
  },

  getHistory: (params = {}, requester) => {
    const boundedParams = buildBoundedPaginationParams(params, 50, 200);
    return requestWithDualRoute("/api/v1/vehicle-admin/history", "/api/vehicle-admin/history", {
      method: "GET",
      params: boundedParams,
    }, requester);
  },

  getVehicleDetail: (id, params = {}, requester) => {
    const boundedParams = {
      takeHistory: params.takeHistory ?? 20,
      ...params,
    };
    return requestWithDualRoute(
      `/api/v1/vehicle-admin/vehicles/${id}`,
      `/api/vehicle-admin/vehicles/${id}`,
      {
        method: "GET",
        params: boundedParams,
      },
      requester
    );
  },

  createVehicle: (data, requester) =>
    requestWithDualRoute("/api/v1/vehicle-admin/vehicles", "/api/vehicle-admin/vehicles", {
      method: "POST",
      data,
    }, requester),

  updateVehicle: (id, data, requester) =>
    requestWithDualRoute(`/api/v1/vehicle-admin/vehicles/${id}`, `/api/vehicle-admin/vehicles/${id}`, {
      method: "PUT",
      data,
    }, requester),

  lockEdit: (id, requester) =>
    requestWithDualRoute(
      `/api/v1/vehicle-admin/vehicles/${id}/lock-edit`,
      `/api/vehicle-admin/vehicles/${id}/lock-edit`,
      { method: "POST" },
      requester
    ),

  unlockEdit: (id, requester) =>
    requestWithDualRoute(
      `/api/v1/vehicle-admin/vehicles/${id}/unlock-edit`,
      `/api/vehicle-admin/vehicles/${id}/unlock-edit`,
      { method: "POST" },
      requester
    ),

  hideVehicle: (id, requester) =>
    requestWithDualRoute(
      `/api/v1/vehicle-admin/vehicles/${id}/hide`,
      `/api/vehicle-admin/vehicles/${id}/hide`,
      { method: "POST" },
      requester
    ),

  updateStatus: (id, status, note, requester) =>
    requestWithDualRoute(
      `/api/v1/vehicle-admin/vehicles/${id}/status`,
      `/api/vehicle-admin/vehicles/${id}/status`,
      {
        method: "POST",
        data: { status, note },
      },
      requester
    ),

  changeOwner: (id, customerId, note, requester) =>
    requestWithDualRoute(
      `/api/v1/vehicle-admin/vehicles/${id}/owner`,
      `/api/vehicle-admin/vehicles/${id}/owner`,
      {
        method: "POST",
        data: { customerId, note },
      },
      requester
    ),

  assignStaff: (id, data, requester) =>
    requestWithDualRoute(
      `/api/v1/vehicle-admin/vehicles/${id}/assign-staff`,
      `/api/vehicle-admin/vehicles/${id}/assign-staff`,
      {
        method: "POST",
        data,
      },
      requester
    ),

  assignBay: (id, data, requester) =>
    requestWithDualRoute(
      `/api/v1/vehicle-admin/vehicles/${id}/assign-bay`,
      `/api/vehicle-admin/vehicles/${id}/assign-bay`,
      {
        method: "POST",
        data,
      },
      requester
    ),

  moveBay: (id, data, requester) =>
    requestWithDualRoute(
      `/api/v1/vehicle-admin/vehicles/${id}/move-bay`,
      `/api/vehicle-admin/vehicles/${id}/move-bay`,
      {
        method: "POST",
        data,
      },
      requester
    ),

  removeBay: (id, data = {}, requester) =>
    requestWithDualRoute(
      `/api/v1/vehicle-admin/vehicles/${id}/remove-bay`,
      `/api/vehicle-admin/vehicles/${id}/remove-bay`,
      {
        method: "POST",
        data,
      },
      requester
    ),
};
