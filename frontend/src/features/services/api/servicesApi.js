import { request } from "../../../app/apiClient";

export const servicesApi = {
  getServiceCategories: () => request({ url: "/api/ServiceCategory", method: "GET" }),
  createServiceCategory: (data) => request({ url: "/api/ServiceCategory", method: "POST", data }),
  updateServiceCategory: (id, data) => request({ url: `/api/ServiceCategory/${id}`, method: "PUT", data }),
  deleteServiceCategory: (id) => request({ url: `/api/ServiceCategory/${id}`, method: "DELETE" }),
  
  getStandardServices: () => request({ url: "/api/StandardService", method: "GET" }),
  createStandardService: (data) => request({ url: "/api/StandardService", method: "POST", data }),
  updateStandardService: (id, data) => request({ url: `/api/StandardService/${id}`, method: "PUT", data }),
  deleteStandardService: (id) => request({ url: `/api/StandardService/${id}`, method: "DELETE" }),
};
