import { request } from "@app/apiClient";

/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route if 404 is encountered.
 */
async function requestWithDualRoute(v1Url, legacyUrl, options = {}) {
  try {
    return await request({ ...options, url: v1Url });
  } catch (err) {
    if ((err?.status === 404 || err?.response?.status === 404) && legacyUrl) {
      return await request({ ...options, url: legacyUrl });
    }
    throw err;
  }
}

export function getMyRetailOrderPage({ page, pageSize, anchorId } = {}) {
  return requestWithDualRoute(
    "/api/v1/retail-orders/customers/current/page",
    "/api/retail-orders/customers/current/page",
    {
      method: "GET",
      params: { page, pageSize, anchorId: anchorId || undefined },
    }
  );
}
