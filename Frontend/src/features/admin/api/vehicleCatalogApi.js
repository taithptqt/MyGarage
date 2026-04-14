import { request } from "@app/apiClient";

export async function getAllVehicleBrands() {
  return request({ url: "/api/vehicle-brands", method: "GET" });
}

export async function createVehicleBrand(payload) {
  return request({
    url: "/api/vehicle-brands",
    method: "POST",
    data: payload
  });
}

export async function updateVehicleBrand(id, payload) {
  return request({
    url: `/api/vehicle-brands/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteVehicleBrand(id) {
  await request({
    url: `/api/vehicle-brands/${id}`,
    method: "DELETE"
  });
  return true;
}

export async function getAllVehicleModels() {
  return request({ url: "/api/vehicle-models", method: "GET" });
}

export async function createVehicleModel(payload) {
  return request({
    url: "/api/vehicle-models",
    method: "POST",
    data: payload
  });
}

export async function updateVehicleModel(id, payload) {
  return request({
    url: `/api/vehicle-models/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteVehicleModel(id) {
  await request({
    url: `/api/vehicle-models/${id}`,
    method: "DELETE"
  });
  return true;
}
