import { request } from "@app/apiClient";

export const inventoryApi = {
  getPartCategories: () => request({ url: "/api/categories", method: "GET" }),
  createPartCategory: (data) => request({ url: "/api/categories", method: "POST", data }),
  updatePartCategory: (id, data) => request({ url: `/api/categories/${id}`, method: "PUT", data }),
  deletePartCategory: (id) => request({ url: `/api/categories/${id}`, method: "DELETE" }),

  getProducts: () => request({ url: "/api/products", method: "GET" }),
  createProduct: (data) => request({ url: "/api/products", method: "POST", data }),
  updateProduct: (id, data) => request({ url: `/api/products/${id}`, method: "PUT", data }),
  deleteProduct: (id) => request({ url: `/api/products/${id}`, method: "DELETE" }),
  uploadProductImage: (productId, file) => {
    const formData = new FormData();
    formData.append("file", file);
    return request({
      url: `/api/admin/parts/${productId}/image`,
      method: "POST",
      data: formData,
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  getSuppliers: () => request({ url: "/api/suppliers", method: "GET" }),
  createSupplier: (data) => request({ url: "/api/suppliers", method: "POST", data }),
  updateSupplier: (id, data) => request({ url: `/api/suppliers/${id}`, method: "PUT", data }),
  deleteSupplier: (id) => request({ url: `/api/suppliers/${id}`, method: "DELETE" }),

  getWarehouses: () => request({ url: "/api/warehouses", method: "GET" }),
  createWarehouse: (data) => request({ url: "/api/warehouses", method: "POST", data }),
  updateWarehouse: (id, data) => request({ url: `/api/warehouses/${id}`, method: "PUT", data }),
  deleteWarehouse: (id) => request({ url: `/api/warehouses/${id}`, method: "DELETE" }),
  getBranches: () => request({ url: "/api/branches", method: "GET" }),
  getVehicleModels: () => request({ url: "/api/vehicle-models", method: "GET" }),

  getInventories: () => request({ url: "/api/inventories", method: "GET" }),
  updateInventory: (id, data) => request({ url: `/api/inventories/${id}`, method: "PUT", data }),

  getProductImages: () => request({ url: "/api/product-images", method: "GET" }),
  createProductImage: (data) => request({ url: "/api/product-images", method: "POST", data }),
  deleteProductImage: (id) => request({ url: `/api/product-images/${id}`, method: "DELETE" }),

  getModelRules: () => request({ url: "/api/model-specific-rules", method: "GET" }),
  createModelRule: (data) => request({ url: "/api/model-specific-rules", method: "POST", data }),
  updateModelRule: (modelId, productId, data) => request({ url: `/api/model-specific-rules/${modelId}/${productId}`, method: "PUT", data }),
  deleteModelRule: (modelId, productId) => request({ url: `/api/model-specific-rules/${modelId}/${productId}`, method: "DELETE" }),

  getOverview: () => request({ url: "/api/inventory-management/dashboards/overview", method: "GET" }),
  getBranchSummary: () => request({ url: "/api/inventory-management/branch-summaries", method: "GET" }),
  getWarehouseStructure: () => request({ url: "/api/inventory-management/warehouse-structures", method: "GET" }),
  getAlerts: () => request({ url: "/api/inventory-management/alert-items", method: "GET" }),
  getTransactions: (params = {}) => request({ url: "/api/inventory-management/stock-transactions", method: "GET", params }),
  getApprovals: (status) => request({ url: "/api/inventory-management/approval-requests", method: "GET", params: status ? { status } : {} }),
  approveRequest: (id, data = {}) => request({ url: `/api/inventory-management/approval-requests/${id}`, method: "PATCH", data: { ...data, decision: "APPROVE" } }),
  rejectRequest: (id, data = {}) => request({ url: `/api/inventory-management/approval-requests/${id}`, method: "PATCH", data: { ...data, decision: "REJECT" } }),
  getAudits: (status) => request({ url: "/api/inventory-management/audit-requests", method: "GET", params: status ? { status } : {} }),
  approveAudit: (id, data = {}) => request({ url: `/api/inventory-management/audit-requests/${id}`, method: "PATCH", data: { ...data, decision: "APPROVE" } }),
  rejectAudit: (id, data = {}) => request({ url: `/api/inventory-management/audit-requests/${id}`, method: "PATCH", data: { ...data, decision: "REJECT" } }),
  getReports: () => request({ url: "/api/inventory-management/analytics-reports", method: "GET" }),
  getAuditLogs: (take = 300) => request({ url: "/api/inventory-management/audit-entries", method: "GET", params: { take } }),
  getSettings: () => request({ url: "/api/inventory-management/configuration", method: "GET" }),
  saveSettings: (data) => request({ url: "/api/inventory-management/configuration", method: "PUT", data }),
};
