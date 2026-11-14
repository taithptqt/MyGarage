async function getRequester(requester) {
  if (typeof requester === "function") return requester;
  const client = await import("@app/apiClient");
  return client.request;
}

/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route if 404 is encountered.
 */
export async function requestWithDualRoute(v1Url, legacyUrl, options = {}, requester) {
  const send = await getRequester(requester);
  try {
    return await send({ ...options, url: v1Url });
  } catch (err) {
    if ((err?.status === 404 || err?.response?.status === 404) && legacyUrl) {
      return await send({ ...options, url: legacyUrl });
    }
    throw err;
  }
}

export const servicesApi = {
  getServiceCategories: (params = {}, requester) =>
    requestWithDualRoute("/api/v1/service-categories", "/api/service-categories", {
      method: "GET",
      params: { page: 1, pageSize: 100, ...params },
    }, requester),

  createServiceCategory: (data, requester) =>
    requestWithDualRoute("/api/v1/service-categories", "/api/service-categories", {
      method: "POST",
      data,
    }, requester),

  updateServiceCategory: (id, data, requester) =>
    requestWithDualRoute(`/api/v1/service-categories/${id}`, `/api/service-categories/${id}`, {
      method: "PUT",
      data,
    }, requester),

  deleteServiceCategory: (id, requester) =>
    requestWithDualRoute(`/api/v1/service-categories/${id}`, `/api/service-categories/${id}`, {
      method: "DELETE",
    }, requester),

  getStandardServices: (branchIdOrParams, maybeParams = {}, requester) => {
    let params = { page: 1, pageSize: 100 };
    if (typeof branchIdOrParams === "object" && branchIdOrParams !== null) {
      params = { ...params, ...branchIdOrParams };
    } else {
      if (branchIdOrParams) params.branchId = branchIdOrParams;
      params = { ...params, ...maybeParams };
    }
    return requestWithDualRoute("/api/v1/standard-services", "/api/standard-services", {
      method: "GET",
      params,
    }, requester);
  },

  getStandardServicePage: (params = {}, requester) =>
    requestWithDualRoute("/api/v1/standard-services/admin-list", "/api/standard-services/admin-list", {
      method: "GET",
      params: { page: 1, pageSize: 20, ...params },
    }, requester),

  createStandardService: (data, requester) =>
    requestWithDualRoute("/api/v1/standard-services", "/api/standard-services", {
      method: "POST",
      data,
    }, requester),

  updateStandardService: (id, data, requester) =>
    requestWithDualRoute(`/api/v1/standard-services/${id}`, `/api/standard-services/${id}`, {
      method: "PUT",
      data,
    }, requester),

  deleteStandardService: (id, requester) =>
    requestWithDualRoute(`/api/v1/standard-services/${id}`, `/api/standard-services/${id}`, {
      method: "DELETE",
    }, requester),
};
