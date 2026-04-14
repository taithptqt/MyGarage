import { request, requestSafe } from "@app/apiClient";

export async function getProducts() {
  return request({ url: "/api/products", method: "GET" });
}

export async function getProductById(productId) {
  return request({ url: `/api/products/${productId}`, method: "GET" });
}

export async function getProductStockAvailability({ productIds, branchId } = {}) {
  const normalizedProductIds = Array.isArray(productIds)
    ? productIds
        .map((value) => Number(value))
        .filter((value) => Number.isFinite(value) && value > 0)
    : [];

  if (!normalizedProductIds.length) {
    return [];
  }

  const params = {
    productIds: normalizedProductIds.join(","),
  };

  if (branchId != null && branchId !== "") {
    params.branchId = branchId;
  }

  return request({
    url: "/api/products/stock-availability",
    method: "GET",
    params,
  });
}

export async function getProductStockAvailabilitySafe({ productIds, branchId } = {}) {
  const normalizedProductIds = Array.isArray(productIds)
    ? productIds
        .map((value) => Number(value))
        .filter((value) => Number.isFinite(value) && value > 0)
    : [];

  if (!normalizedProductIds.length) {
    return { ok: true, status: 200, data: [] };
  }

  const params = {
    productIds: normalizedProductIds.join(","),
  };

  if (branchId != null && branchId !== "") {
    params.branchId = branchId;
  }

  return requestSafe({
    url: "/api/products/stock-availability",
    method: "GET",
    params,
  });
}

export async function getProductRetailStockByBranches(productId) {
  const id = Number(productId);
  if (!Number.isFinite(id) || id <= 0) {
    return { productId: id, branches: [] };
  }

  return request({
    url: `/api/products/${id}/retail-stock-by-branches`,
    method: "GET",
  });
}

export async function getCategories() {
  return request({ url: "/api/categories", method: "GET" });
}

export async function getBranches() {
  return request({ url: "/api/branches", method: "GET" });
}

export async function getProductImages(options = {}) {
  const query = {};
  if (options?.productId != null && options.productId !== "") {
    query.productId = options.productId;
  }

  return request({ url: "/api/product-images", method: "GET", params: query });
}

export async function createRetailOrder(payload) {
  return request({ url: "/api/retail-orders", method: "POST", data: payload });
}

export async function createRetailOrderItem(payload) {
  return request({ url: "/api/retail-order-items", method: "POST", data: payload });
}

export async function checkoutRetailOrder(payload) {
  return request({ url: "/api/retail-orders/checkout", method: "POST", data: payload });
}

export async function getMyRetailOrders() {
  return request({ url: "/api/retail-orders/customers/current", method: "GET" });
}

export async function getRetailOrderById(retailOrderId) {
  return request({ url: `/api/retail-orders/${retailOrderId}`, method: "GET" });
}


