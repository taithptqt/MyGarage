export function buildBoundedPaginationParams(params = {}, defaultPageSize = 25, maxPageSize = 200) {
  const pageNumber = Number(params?.page);
  const safePage = Number.isFinite(pageNumber) && pageNumber > 0 ? Math.trunc(pageNumber) : 1;

  const sizeNumber = Number(params?.pageSize);
  const requestedSize = Number.isFinite(sizeNumber) && sizeNumber > 0 ? Math.trunc(sizeNumber) : defaultPageSize;
  const safePageSize = Math.min(Math.max(1, requestedSize), maxPageSize);

  return {
    ...params,
    page: safePage,
    pageSize: safePageSize,
  };
}

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

const V1_BASE = "/api/v1/customers/me";
const LEGACY_BASE = "/api/customers/me";

export function getCustomerDashboard(requester) {
  return requestWithDualRoute(
    `${V1_BASE}/dashboard`,
    `${LEGACY_BASE}/dashboard`,
    { method: "GET" },
    requester
  );
}

export function getCustomerVehicles(requester) {
  return requestWithDualRoute(
    `${V1_BASE}/vehicles`,
    `${LEGACY_BASE}/vehicles`,
    { method: "GET" },
    requester
  );
}

export function createCustomerVehicle(body, requester) {
  return requestWithDualRoute(
    `${V1_BASE}/vehicles`,
    `${LEGACY_BASE}/vehicles`,
    { method: "POST", data: body },
    requester
  );
}

export function updateCustomerVehicle(vehicleId, body, requester) {
  return requestWithDualRoute(
    `${V1_BASE}/vehicles/${vehicleId}`,
    `${LEGACY_BASE}/vehicles/${vehicleId}`,
    { method: "PUT", data: body },
    requester
  );
}

export function getVehicleHistory(vehicleId, paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const rawParams = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const boundedParams = buildBoundedPaginationParams(rawParams, 20, 100);
  return requestWithDualRoute(
    `${V1_BASE}/vehicles/${vehicleId}/history`,
    `${LEGACY_BASE}/vehicles/${vehicleId}/history`,
    { method: "GET", params: boundedParams },
    requester
  );
}

export function getCustomerNotifications(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const rawParams = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;

  const bounded = buildBoundedPaginationParams(rawParams, 25, 200);
  const params = {
    page: bounded.page,
    pageSize: bounded.pageSize,
  };
  if (bounded.unreadOnly) params.unreadOnly = true;

  return requestWithDualRoute(
    `${V1_BASE}/notifications`,
    `${LEGACY_BASE}/notifications`,
    { method: "GET", params },
    requester
  );
}

export function markNotificationRead(id, requester) {
  return requestWithDualRoute(
    `${V1_BASE}/notifications/${id}/read`,
    `${LEGACY_BASE}/notifications/${id}/read`,
    { method: "POST" },
    requester
  );
}

export function markAllNotificationsRead(requester) {
  return requestWithDualRoute(
    `${V1_BASE}/notifications/read-all`,
    `${LEGACY_BASE}/notifications/read-all`,
    { method: "POST" },
    requester
  );
}

export function getVehicleLookups(requester) {
  return requestWithDualRoute(
    `${V1_BASE}/vehicle-lookups`,
    `${LEGACY_BASE}/vehicle-lookups`,
    { method: "GET" },
    requester
  );
}

export function getServiceOrderLinkContext(serviceOrderId, requester) {
  return requestWithDualRoute(
    `${V1_BASE}/service-orders/${serviceOrderId}/link-context`,
    `${LEGACY_BASE}/service-orders/${serviceOrderId}/link-context`,
    { method: "GET" },
    requester
  );
}
