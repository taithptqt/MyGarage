import { request } from "@app/apiClient";

export async function getAllCustomers(branchId) {
  return request({
    url: "/api/customers",
    method: "GET",
    params: branchId ? { branchId } : undefined
  });
}

export async function getCustomerById(id) {
  return request({
    url: `/api/customers/${id}`,
    method: "GET",
  });
}

export async function createCustomer(payload) {
  return request({
    url: "/api/customers",
    method: "POST",
    data: payload
  });
}

export async function updateCustomer(id, payload) {
  return request({
    url: `/api/customers/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteCustomer(id) {
  await request({
    url: `/api/customers/${id}`,
    method: "DELETE"
  });
  return true;
}

export async function getAllCustomerTiers() {
  return request({ url: "/api/customer-tiers", method: "GET" });
}

export async function getAllCustomerVehicles() {
  return request({ url: "/api/customer-vehicles", method: "GET" });
}

export async function createCustomerVehicle(payload) {
  return request({
    url: "/api/customer-vehicles",
    method: "POST",
    data: payload
  });
}

export async function updateCustomerVehicle(id, payload) {
  return request({
    url: `/api/customer-vehicles/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteCustomerVehicle(id) {
  await request({
    url: `/api/customer-vehicles/${id}`,
    method: "DELETE"
  });
  return true;
}

export async function getAllVehicles() {
  return request({ url: "/api/vehicles", method: "GET" });
}

export async function getVehicleById(id) {
  return request({
    url: `/api/vehicles/${id}`,
    method: "GET",
  });
}

export async function createVehicle(payload) {
  return request({
    url: "/api/vehicles",
    method: "POST",
    data: payload
  });
}

export async function updateVehicle(id, payload) {
  return request({
    url: `/api/vehicles/${id}`,
    method: "PUT",
    data: payload
  });
}
