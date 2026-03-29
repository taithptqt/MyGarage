import { request } from "../../../app/apiClient";

export async function getProducts() {
  return request({ url: "/api/Product", method: "GET" });
}

export async function getCategories() {
  return request({ url: "/api/Category", method: "GET" });
}

export async function getBranches() {
  return request({ url: "/api/Branch", method: "GET" });
}

export async function createRetailOrder(payload) {
  return request({ url: "/api/RetailOrder", method: "POST", data: payload });
}

export async function createRetailOrderItem(payload) {
  return request({ url: "/api/RetailOrderItem", method: "POST", data: payload });
}

export async function checkoutRetailOrder(payload) {
  return request({ url: "/api/RetailOrder/checkout", method: "POST", data: payload });
}

export async function getMyRetailOrders() {
  return request({ url: "/api/RetailOrder/mine", method: "GET" });
}

