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
    requestWithDualRoute("/api/v1/admin/vehicles/lookups", "/api/v1/admin/vehicles/lookups", {
      method: "GET",
      params,
    }, requester),

  getList: (params = {}, requester) => {
    const boundedParams = buildBoundedPaginationParams(params, 20, 200);
    return requestWithDualRoute("/api/v1/admin/vehicles", "/api/v1/admin/vehicles", {
      method: "GET",
      params: boundedParams,
    }, requester);
  },

  getSummary: (params = {}, requester) =>
    requestWithDualRoute("/api/v1/admin/vehicles/summary", "/api/v1/admin/vehicles/summary", {
      method: "GET",
      params,
    }, requester),

  getAlerts: (params = {}, requester) => {
    const boundedParams = buildBoundedPaginationParams(params, 50, 200);
    return requestWithDualRoute("/api/v1/admin/vehicles/alerts", "/api/v1/admin/vehicles/alerts", {
      method: "GET",
      params: boundedParams,
    }, requester);
  },

  getHistory: (params = {}, requester) => {
    const boundedParams = buildBoundedPaginationParams(params, 50, 200);
    return requestWithDualRoute("/api/v1/admin/vehicles/history", "/api/v1/admin/vehicles/history", {
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
      `/api/v1/admin/vehicles/${id}`,
      `/api/v1/admin/vehicles/${id}`,
      {
        method: "GET",
        params: boundedParams,
      },
      requester
    );
  },

  createVehicle: (data, requester) =>
    requestWithDualRoute("/api/v1/admin/vehicles", "/api/v1/admin/vehicles", {
      method: "POST",
      data,
    }, requester),

  updateVehicle: (id, data, requester) =>
    requestWithDualRoute(`/api/v1/admin/vehicles/${id}`, `/api/v1/admin/vehicles/${id}`, {
      method: "PUT",
      data,
    }, requester),

  lockEdit: (id, requester) =>
    requestWithDualRoute(
      `/api/v1/admin/vehicles/${id}/edit-locks`,
      `/api/v1/admin/vehicles/${id}/edit-locks`,
      { method: "PUT" },
      requester
    ),

  unlockEdit: (id, requester) =>
    requestWithDualRoute(
      `/api/v1/admin/vehicles/${id}/edit-locks`,
      `/api/v1/admin/vehicles/${id}/edit-locks`,
      { method: "DELETE" },
      requester
    ),

  hideVehicle: (id, requester) =>
    requestWithDualRoute(
      `/api/v1/admin/vehicles/${id}`,
      `/api/v1/admin/vehicles/${id}`,
      { method: "DELETE" },
      requester
    ),

  updateStatus: (id, status, note, requester) =>
    requestWithDualRoute(
      `/api/v1/admin/vehicles/${id}/status`,
      `/api/v1/admin/vehicles/${id}/status`,
      {
        method: "PATCH",
        data: { status, note },
      },
      requester
    ),

  changeOwner: (id, customerId, note, requester) =>
    requestWithDualRoute(
      `/api/v1/admin/vehicles/${id}/owner`,
      `/api/v1/admin/vehicles/${id}/owner`,
      {
        method: "PATCH",
        data: { customerId, note },
      },
      requester
    ),

  assignStaff: (id, data, requester) =>
    requestWithDualRoute(
      `/api/v1/admin/vehicles/${id}/staff-assignments`,
      `/api/v1/admin/vehicles/${id}/staff-assignments`,
      {
        method: "POST",
        data,
      },
      requester
    ),

  assignBay: (id, data, requester) =>
    requestWithDualRoute(
      `/api/v1/admin/vehicles/${id}/bay-assignments`,
      `/api/v1/admin/vehicles/${id}/bay-assignments`,
      {
        method: "POST",
        data,
      },
      requester
    ),

  moveBay: (id, data, requester) =>
    requestWithDualRoute(
      `/api/v1/admin/vehicles/${id}/bay-transfers`,
      `/api/v1/admin/vehicles/${id}/bay-transfers`,
      {
        method: "POST",
        data,
      },
      requester
    ),

  removeBay: (id, data = {}, requester) =>
    requestWithDualRoute(
      `/api/v1/admin/vehicles/${id}/bay-removals`,
      `/api/v1/admin/vehicles/${id}/bay-removals`,
      {
        method: "POST",
        data,
      },
      requester
    ),
};
