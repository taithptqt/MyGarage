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

  getSuppliers: () => request({ url: "/api/Supplier", method: "GET" }),
  createSupplier: (data) => request({ url: "/api/Supplier", method: "POST", data }),
  updateSupplier: (id, data) => request({ url: `/api/Supplier/${id}`, method: "PUT", data }),
  deleteSupplier: (id) => request({ url: `/api/Supplier/${id}`, method: "DELETE" }),

  getWarehouses: () => request({ url: "/api/Warehouse", method: "GET" }),
  createWarehouse: (data) => request({ url: "/api/Warehouse", method: "POST", data }),
  updateWarehouse: (id, data) => request({ url: `/api/Warehouse/${id}`, method: "PUT", data }),
  deleteWarehouse: (id) => request({ url: `/api/Warehouse/${id}`, method: "DELETE" }),
  getBranches: () => request({ url: "/api/Branch", method: "GET" }),
  getVehicleModels: () => request({ url: "/api/VehicleModel", method: "GET" }),

  getInventories: () => request({ url: "/api/Inventory", method: "GET" }),
  updateInventory: (id, data) => request({ url: `/api/Inventory/${id}`, method: "PUT", data }),

  getProductImages: () => request({ url: "/api/ProductImage", method: "GET" }),
  createProductImage: (data) => request({ url: "/api/ProductImage", method: "POST", data }),
  deleteProductImage: (id) => request({ url: `/api/ProductImage/${id}`, method: "DELETE" }),

  getModelRules: () => request({ url: "/api/ModelSpecificRule", method: "GET" }),
  createModelRule: (data) => request({ url: "/api/ModelSpecificRule", method: "POST", data }),
  updateModelRule: (modelId, productId, data) => request({ url: `/api/ModelSpecificRule/${modelId}/${productId}`, method: "PUT", data }),
  deleteModelRule: (modelId, productId) => request({ url: `/api/ModelSpecificRule/${modelId}/${productId}`, method: "DELETE" }),

  getOverview: () => request({ url: "/api/inventory-admin/overview", method: "GET" }),
  getBranchSummary: () => request({ url: "/api/inventory-admin/branch-summary", method: "GET" }),
  getWarehouseStructure: () => request({ url: "/api/inventory-admin/warehouse-structure", method: "GET" }),
  getAlerts: () => request({ url: "/api/inventory-admin/alerts", method: "GET" }),
  getTransactions: (params = {}) => request({ url: "/api/inventory-admin/transactions", method: "GET", params }),
  getApprovals: (status) => request({ url: "/api/inventory-admin/approvals", method: "GET", params: status ? { status } : {} }),
  approveRequest: (id, data = {}) => request({ url: `/api/inventory-admin/approvals/${id}/approve`, method: "POST", data }),
  rejectRequest: (id, data = {}) => request({ url: `/api/inventory-admin/approvals/${id}/reject`, method: "POST", data }),
  getAudits: (status) => request({ url: "/api/inventory-admin/audits", method: "GET", params: status ? { status } : {} }),
  approveAudit: (id, data = {}) => request({ url: `/api/inventory-admin/audits/${id}/approve`, method: "POST", data }),
  rejectAudit: (id, data = {}) => request({ url: `/api/inventory-admin/audits/${id}/reject`, method: "POST", data }),
  getReports: () => request({ url: "/api/inventory-admin/reports", method: "GET" }),
  getAuditLogs: (take = 300) => request({ url: "/api/inventory-admin/audit-log", method: "GET", params: { take } }),
  getSettings: () => request({ url: "/api/inventory-admin/settings", method: "GET" }),
  saveSettings: (data) => request({ url: "/api/inventory-admin/settings", method: "PUT", data }),
};
