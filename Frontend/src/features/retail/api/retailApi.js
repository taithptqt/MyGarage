import { request, requestSafe } from "@app/apiClient";
import { loadStockBatches } from "./stockBatches";

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

export async function getProducts() {
  return requestWithDualRoute("/api/v1/products", "/api/products", { method: "GET" });
}

export async function getRetailCatalog(params) {
  return requestWithDualRoute("/api/v1/products/retail-catalog", "/api/products/retail-catalog", {
    method: "GET",
    params,
  });
}

export async function getProductById(productId) {
  return requestWithDualRoute(`/api/v1/products/${productId}`, `/api/products/${productId}`, {
    method: "GET",
  });
}

export async function getProductStockAvailability({ productIds, branchId } = {}) {
  const result = await loadStockBatches(productIds, async (ids) => {
    const params = { productIds: ids.join(",") };
    if (branchId != null && branchId !== "") params.branchId = branchId;
    const data = await requestWithDualRoute(
      "/api/v1/products/stock-availability",
      "/api/products/stock-availability",
      { method: "GET", params }
    );
    return { ok: true, data };
  });
  if (!result.ok) throw new Error(result.data?.message || "Invalid stock response.");
  return result.data;
}

export async function getProductStockAvailabilitySafe({ productIds, branchId } = {}) {
  return loadStockBatches(productIds, (ids) => {
    const params = { productIds: ids.join(",") };
    if (branchId != null && branchId !== "") params.branchId = branchId;
    return requestSafeWithDualRoute(
      "/api/v1/products/stock-availability",
      "/api/products/stock-availability",
      { method: "GET", params }
    );
  });
}

export async function getProductRetailStockByBranches(productId) {
  const id = Number(productId);
  if (!Number.isFinite(id) || id <= 0) {
    return { productId: id, branches: [] };
  }

  return requestWithDualRoute(
    `/api/v1/products/${id}/retail-stock-by-branches`,
    `/api/products/${id}/retail-stock-by-branches`,
    {
      method: "GET",
    }
  );
}

export async function getCategories() {
  return requestWithDualRoute("/api/v1/categories", "/api/categories", { method: "GET" });
}

export async function getBranches() {
  return requestWithDualRoute("/api/v1/branches", "/api/branches", { method: "GET" });
}

export async function getProductImages(options = {}) {
  const query = {};
  if (options?.productId != null && options.productId !== "") {
    query.productId = options.productId;
  }

  return requestWithDualRoute("/api/v1/product-images", "/api/product-images", {
    method: "GET",
    params: query,
  });
}

export async function createRetailOrder(payload) {
  return requestWithDualRoute("/api/v1/retail-orders", "/api/retail-orders", {
    method: "POST",
    data: payload,
  });
}

export async function createRetailOrderItem(payload) {
  return requestWithDualRoute("/api/v1/retail-order-items", "/api/retail-order-items", {
    method: "POST",
    data: payload,
  });
}

export async function checkoutRetailOrder(payload) {
  return requestWithDualRoute("/api/v1/retail-orders/checkout", "/api/retail-orders/checkout", {
    method: "POST",
    data: payload,
  });
}

export async function getMyRetailOrders() {
  return requestWithDualRoute(
    "/api/v1/retail-orders/customers/current",
    "/api/retail-orders/customers/current",
    { method: "GET" }
  );
}

export async function getRetailOrderById(retailOrderId) {
  return requestWithDualRoute(
    `/api/v1/retail-orders/${retailOrderId}`,
    `/api/retail-orders/${retailOrderId}`,
    { method: "GET" }
  );
}
