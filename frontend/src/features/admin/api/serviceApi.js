import { servicesApi } from "@features/services/api/servicesApi";

export async function getAllServiceCategories() {
  return servicesApi.getServiceCategories();
}

export async function getAllStandardServices(branchId) {
  return servicesApi.getStandardServices(branchId);
}

export async function createStandardService(payload) {
  return servicesApi.createStandardService(payload);
}

export async function updateStandardService(id, payload) {
  return servicesApi.updateStandardService(id, payload);
}

export async function deleteStandardService(id) {
  await servicesApi.deleteStandardService(id);
  return true;
}
