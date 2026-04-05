import { request } from "@app/apiClient";

export async function getProducts() {
  return request({ url: "/api/products", method: "GET" });
}

export async function getCategories() {
  return request({ url: "/api/categories", method: "GET" });
}

export async function getBranches() {
  return request({ url: "/api/branches", method: "GET" });
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


