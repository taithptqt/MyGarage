import { servicesApi, requestWithDualRoute } from "../../services/api/servicesApi.js";

export { requestWithDualRoute };

export async function getAllServiceCategories(params = {}, requester) {
  return servicesApi.getServiceCategories(params, requester);
}

export async function getAllStandardServices(branchId, params = {}, requester) {
  return servicesApi.getStandardServices(branchId, params, requester);
}

export async function getStandardServicePage(params = {}, requester) {
  return servicesApi.getStandardServicePage(params, requester);
}

export async function createStandardService(payload, requester) {
  return servicesApi.createStandardService(payload, requester);
}

export async function updateStandardService(id, payload, requester) {
  return servicesApi.updateStandardService(id, payload, requester);
}

export async function deleteStandardService(id, requester) {
  await servicesApi.deleteStandardService(id, requester);
  return true;
}
