import { request } from "../../../app/apiClient";

export async function getAllCustomers() {
  return request({ url: "/api/Customer", method: "GET" });
}

export async function createCustomer(payload) {
  return request({
    url: "/api/Customer",
    method: "POST",
    data: payload
  });
}

export async function updateCustomer(id, payload) {
  return request({
    url: `/api/Customer/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteCustomer(id) {
  await request({
    url: `/api/Customer/${id}`,
    method: "DELETE"
  });
  return true;
}

export async function getAllCustomerTiers() {
  return request({ url: "/api/CustomerTier", method: "GET" });
}

export async function getAllCustomerVehicles() {
  return request({ url: "/api/CustomerVehicle", method: "GET" });
}

export async function createCustomerVehicle(payload) {
  return request({
    url: "/api/CustomerVehicle",
    method: "POST",
    data: payload
  });
}

export async function updateCustomerVehicle(id, payload) {
  return request({
    url: `/api/CustomerVehicle/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteCustomerVehicle(id) {
  await request({
    url: `/api/CustomerVehicle/${id}`,
    method: "DELETE"
  });
  return true;
}

export async function getAllVehicles() {
  return request({ url: "/api/Vehicle", method: "GET" });
}

export async function createVehicle(payload) {
  return request({
    url: "/api/Vehicle",
    method: "POST",
    data: payload
  });
}

export async function updateVehicle(id, payload) {
  return request({
    url: `/api/Vehicle/${id}`,
    method: "PUT",
    data: payload
  });
}
