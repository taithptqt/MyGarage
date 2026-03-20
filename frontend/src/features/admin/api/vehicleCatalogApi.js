import { request } from "../../../app/apiClient";

export async function getAllVehicleBrands() {
  return request({ url: "/api/VehicleBrand", method: "GET" });
}

export async function createVehicleBrand(payload) {
  return request({
    url: "/api/VehicleBrand",
    method: "POST",
    data: payload
  });
}

export async function updateVehicleBrand(id, payload) {
  return request({
    url: `/api/VehicleBrand/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteVehicleBrand(id) {
  await request({
    url: `/api/VehicleBrand/${id}`,
    method: "DELETE"
  });
  return true;
}

export async function getAllVehicleModels() {
  return request({ url: "/api/VehicleModel", method: "GET" });
}

export async function createVehicleModel(payload) {
  return request({
    url: "/api/VehicleModel",
    method: "POST",
    data: payload
  });
}

export async function updateVehicleModel(id, payload) {
  return request({
    url: `/api/VehicleModel/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteVehicleModel(id) {
  await request({
    url: `/api/VehicleModel/${id}`,
    method: "DELETE"
  });
  return true;
}
