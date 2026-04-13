import { inventoryApi as inventoryCoreApi } from "@features/inventory/api/inventoryApi";

export const inventoryApi = {
  getPartCategories: inventoryCoreApi.getPartCategories,
  createPartCategory: inventoryCoreApi.createPartCategory,
  updatePartCategory: inventoryCoreApi.updatePartCategory,
  deletePartCategory: inventoryCoreApi.deletePartCategory,
  getProducts: inventoryCoreApi.getProducts,
  createProduct: inventoryCoreApi.createProduct,
  updateProduct: inventoryCoreApi.updateProduct,
  deleteProduct: inventoryCoreApi.deleteProduct
};
