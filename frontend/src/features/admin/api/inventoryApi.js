import { request } from "../../../app/apiClient";

export const inventoryApi = {
  getPartCategories: () => request({ url: "/api/Category", method: "GET" }),
  createPartCategory: (data) => request({ url: "/api/Category", method: "POST", data }),
  updatePartCategory: (id, data) => request({ url: `/api/Category/${id}`, method: "PUT", data }),
  deletePartCategory: (id) => request({ url: `/api/Category/${id}`, method: "DELETE" }),
  
  getProducts: () => request({ url: "/api/Product", method: "GET" }),
  createProduct: (data) => request({ url: "/api/Product", method: "POST", data }),
  updateProduct: (id, data) => request({ url: `/api/Product/${id}`, method: "PUT", data }),
  deleteProduct: (id) => request({ url: `/api/Product/${id}`, method: "DELETE" }),
};
