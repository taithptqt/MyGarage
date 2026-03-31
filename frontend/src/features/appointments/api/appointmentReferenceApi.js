import { request } from "../../../app/apiClient";
import { REFERENCE_ROUTES } from "./referenceRoutes";

export async function getAllBranches() {
  return request({ url: REFERENCE_ROUTES.branches, method: "GET" });
}

export async function getAllStandardServices() {
  return request({ url: REFERENCE_ROUTES.standardServices, method: "GET" });
}

export async function getVehicleBrands() {
  return request({ url: REFERENCE_ROUTES.vehicleBrands, method: "GET" });
}

export async function getVehicleModels() {
  return request({ url: REFERENCE_ROUTES.vehicleModels, method: "GET" });
}

export async function getAllProducts() {
  return request({ url: REFERENCE_ROUTES.products, method: "GET" });
}

export async function getAllCategories() {
  return request({ url: REFERENCE_ROUTES.categories, method: "GET" });
}

export async function getAllServiceCategories() {
  return request({ url: REFERENCE_ROUTES.serviceCategories, method: "GET" });
}

export async function getAllCustomers() {
  return request({ url: REFERENCE_ROUTES.customers, method: "GET" });
}

export async function updateCustomer(id, payload) {
  return request({
    url: `${REFERENCE_ROUTES.customers}/${id}`,
    method: "PUT",
    data: payload,
  });
}

export async function getAllCustomerVehicles() {
  return request({ url: REFERENCE_ROUTES.customerVehicles, method: "GET" });
}

export async function getAllVehicles() {
  return request({ url: REFERENCE_ROUTES.vehicles, method: "GET" });
}

export async function createVehicle(payload) {
  return request({
    url: REFERENCE_ROUTES.vehicles,
    method: "POST",
    data: payload,
  });
}

export async function createCustomerVehicle(payload) {
  return request({
    url: REFERENCE_ROUTES.customerVehicles,
    method: "POST",
    data: payload,
  });
}
