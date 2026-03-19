import { request } from "../../../app/apiClient";

export async function getAllServiceCategories() {
  return request({ url: "/api/ServiceCategory", method: "GET" });
}

export async function getAllStandardServices() {
  return request({ url: "/api/StandardService", method: "GET" });
}

export async function createStandardService(payload) {
  return request({
    url: "/api/StandardService",
    method: "POST",
    data: payload
  });
}

export async function updateStandardService(id, payload) {
  return request({
    url: `/api/StandardService/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteStandardService(id) {
  await request({
    url: `/api/StandardService/${id}`,
    method: "DELETE"
  });
  return true;
}
