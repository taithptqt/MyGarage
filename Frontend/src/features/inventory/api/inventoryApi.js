import { request } from "@app/apiClient";

export const inventoryApi = {
  getCatalogPage: (kind, page, pageSize) => request({ url: `/api/v1/inventory-management/catalog/${kind}`, method: "GET", params: { page, pageSize } }),
  exportCatalogProducts: () => request({ url: "/api/v1/inventory-management/catalog/products/export", method: "GET", responseType: "blob" }),
  getPartCategories: () => request({ url: "/api/v1/categories", method: "GET" }),
  createPartCategory: (data) => request({ url: "/api/v1/categories", method: "POST", data }),
  updatePartCategory: (id, data) => request({ url: `/api/v1/categories/${id}`, method: "PUT", data }),
  deletePartCategory: (id) => request({ url: `/api/v1/categories/${id}`, method: "DELETE" }),

  getProducts: () => request({ url: "/api/v1/products", method: "GET" }),
  createProduct: (data) => request({ url: "/api/v1/products", method: "POST", data }),
  updateProduct: (id, data) => request({ url: `/api/v1/products/${id}`, method: "PUT", data }),
  deleteProduct: (id) => request({ url: `/api/v1/products/${id}`, method: "DELETE" }),
  uploadProductImage: (productId, file) => {
    const formData = new FormData();
    formData.append("file", file);
    return request({
      url: `/api/v1/admin/products/${productId}/image`,
      method: "POST",
      data: formData,
    });
  },

  appendProductGalleryImage: (productId, file) => {
    const formData = new FormData();
    formData.append("file", file);
    return request({
      url: `/api/v1/admin/products/${productId}/images`,
      method: "POST",
      data: formData,
    });
  },

  getSuppliers: () => request({ url: "/api/v1/suppliers", method: "GET" }),
  createSupplier: (data) => request({ url: "/api/v1/suppliers", method: "POST", data }),
  updateSupplier: (id, data) => request({ url: `/api/v1/suppliers/${id}`, method: "PUT", data }),
  deleteSupplier: (id) => request({ url: `/api/v1/suppliers/${id}`, method: "DELETE" }),

  getWarehouses: () => request({ url: "/api/v1/warehouses", method: "GET" }),
  createWarehouse: (data) => request({ url: "/api/v1/warehouses", method: "POST", data }),
  updateWarehouse: (id, data) => request({ url: `/api/v1/warehouses/${id}`, method: "PUT", data }),
  deleteWarehouse: (id) => request({ url: `/api/v1/warehouses/${id}`, method: "DELETE" }),
  getBranches: () => request({ url: "/api/v1/branches", method: "GET" }),
  getVehicleModels: () => request({ url: "/api/v1/vehicle-models", method: "GET" }),

  getInventories: () => request({ url: "/api/v1/inventories", method: "GET" }),
  updateInventory: (id, data) => request({ url: `/api/v1/inventories/${id}`, method: "PUT", data }),

  getProductImages: (params = {}) => request({ url: "/api/v1/product-images", method: "GET", params }),
  createProductImage: (data) => request({ url: "/api/v1/product-images", method: "POST", data }),
  deleteProductImage: (id) => request({ url: `/api/v1/product-images/${id}`, method: "DELETE" }),

  getModelRules: () => request({ url: "/api/v1/model-specific-rules", method: "GET" }),
  createModelRule: (data) => request({ url: "/api/v1/model-specific-rules", method: "POST", data }),
  updateModelRule: (modelId, productId, data) => request({ url: `/api/v1/model-specific-rules/${modelId}/${productId}`, method: "PUT", data }),
  deleteModelRule: (modelId, productId) => request({ url: `/api/v1/model-specific-rules/${modelId}/${productId}`, method: "DELETE" }),

  getOverview: () => request({ url: "/api/v1/inventory-management/dashboards/overview", method: "GET" }),
  getBranchSummary: () => request({ url: "/api/v1/inventory-management/branch-summaries", method: "GET" }),
  getWarehouseStructure: (params = {}, maybePageSize) => {
    const query = typeof params === "number"
      ? { page: params, pageSize: maybePageSize || 10 }
      : (params || {});
    return request({ url: "/api/v1/inventory-management/warehouse-structures", method: "GET", params: query });
  },
  getAlerts: () => request({ url: "/api/v1/inventory-management/alert-items", method: "GET" }),
  getTransactions: (params = {}) => request({ url: "/api/v1/inventory-management/stock-transactions", method: "GET", params }),
  getApprovals: (status) => request({ url: "/api/v1/inventory-management/approval-requests", method: "GET", params: status ? { status } : {} }),
  approveRequest: (id, data = {}) => request({ url: `/api/v1/inventory-management/approval-requests/${id}`, method: "PATCH", data: { ...data, decision: "APPROVE" } }),
  rejectRequest: (id, data = {}) => request({ url: `/api/v1/inventory-management/approval-requests/${id}`, method: "PATCH", data: { ...data, decision: "REJECT" } }),
  getAudits: (status) => request({ url: "/api/v1/inventory-management/audit-requests", method: "GET", params: status ? { status } : {} }),
  approveAudit: (id, data = {}) => request({ url: `/api/v1/inventory-management/audit-requests/${id}`, method: "PATCH", data: { ...data, decision: "APPROVE" } }),
  rejectAudit: (id, data = {}) => request({ url: `/api/v1/inventory-management/audit-requests/${id}`, method: "PATCH", data: { ...data, decision: "REJECT" } }),
  getReports: (params = {}) => request({ url: "/api/v1/inventory-management/analytics-reports", method: "GET", params }),
  getAuditLogs: (take = 300) => {
    const query = typeof take === "object" ? take : { take };
    return request({ url: "/api/v1/inventory-management/audit-entries", method: "GET", params: query });
  },
  getSettings: () => request({ url: "/api/v1/inventory-management/configuration", method: "GET" }),
  saveSettings: (data) => request({ url: "/api/v1/inventory-management/configuration", method: "PUT", data }),
};
