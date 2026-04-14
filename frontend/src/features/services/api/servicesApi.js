import { request } from "@app/apiClient";

export const servicesApi = {
  getServiceCategories: () => request({ url: "/api/service-categories", method: "GET" }),
  createServiceCategory: (data) => request({ url: "/api/service-categories", method: "POST", data }),
  updateServiceCategory: (id, data) => request({ url: `/api/service-categories/${id}`, method: "PUT", data }),
  deleteServiceCategory: (id) => request({ url: `/api/service-categories/${id}`, method: "DELETE" }),
  
  getStandardServices: (branchId) =>
    request({
      url: "/api/standard-services",
      method: "GET",
      params: branchId ? { branchId } : undefined
    }),
  createStandardService: (data) => request({ url: "/api/standard-services", method: "POST", data }),
  updateStandardService: (id, data) => request({ url: `/api/standard-services/${id}`, method: "PUT", data }),
  deleteStandardService: (id) => request({ url: `/api/standard-services/${id}`, method: "DELETE" }),
};
