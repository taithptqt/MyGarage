import { request } from "../../../app/apiClient";

// Named exports for compatibility with existing admin components
export async function getAllServiceCategories() {
  return request({ url: "/api/ServiceCategory", method: "GET" });
}

export async function getAllStandardServices() {
  return request({ url: "/api/StandardService", method: "GET" });
}

export async function createStandardService(payload) {
  return request({ url: "/api/StandardService", method: "POST", data: payload });
}

export async function updateStandardService(id, payload) {
  return request({ url: `/api/StandardService/${id}`, method: "PUT", data: payload });
}

export async function deleteStandardService(id) {
  await request({ url: `/api/StandardService/${id}`, method: "DELETE" });
  return true;
}

// Object export for compatibility with Horizon views
export const serviceApi = {
  getServiceCategories: getAllServiceCategories,
  createServiceCategory: (data) => request({ url: "/api/ServiceCategory", method: "POST", data }),
  updateServiceCategory: (id, data) => request({ url: `/api/ServiceCategory/${id}`, method: "PUT", data }),
  deleteServiceCategory: (id) => request({ url: `/api/ServiceCategory/${id}`, method: "DELETE" }),
  
  getStandardServices: getAllStandardServices,
  createStandardService,
  updateStandardService,
  deleteStandardService,
};
