import { request, requestSafe } from "@app/apiClient";

/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route if 404 is encountered.
 */
export async function requestWithDualRoute(v1Url, legacyUrl, options = {}) {
  try {
    return await request({ ...options, url: v1Url });
  } catch (err) {
    if ((err?.status === 404 || err?.response?.status === 404) && legacyUrl) {
      return await request({ ...options, url: legacyUrl });
    }
    throw err;
  }
}

/**
 * Safe version of requestWithDualRoute using requestSafe.
 */
export async function requestSafeWithDualRoute(v1Url, legacyUrl, options = {}) {
  const result = await requestSafe({ ...options, url: v1Url });
  if (!result.ok && (result.status === 404 || result.response?.status === 404) && legacyUrl) {
    return await requestSafe({ ...options, url: legacyUrl });
  }
  return result;
}

export async function createPayosOrder(payload) {
  return requestSafeWithDualRoute("/api/v1/orders", "/api/orders", {
    method: "POST",
    data: payload
  });
}

export async function getPayments({ page, pageSize } = {}) {
  return requestSafeWithDualRoute("/api/v1/payments/mine", "/api/payments/mine", {
    method: "GET",
    params: page == null && pageSize == null ? undefined : { page, pageSize }
  });
}

export async function getPaymentStatus(paymentId) {
  return requestSafeWithDualRoute(
    `/api/v1/payments/${paymentId}/status`,
    `/api/payments/${paymentId}/status`,
    {
      method: "GET"
    }
  );
}

export async function syncOrder(orderId) {
  return requestSafeWithDualRoute(
    `/api/v1/orders/${orderId}`,
    `/api/orders/${orderId}`,
    {
      method: "GET"
    }
  );
}
