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

export async function getAllVehicleBrands(params = {}, requester) {
  return requestWithDualRoute("/api/v1/vehicle-brands", "/api/vehicle-brands", {
    method: "GET",
    params: { page: 1, pageSize: 200, ...params },
  }, requester);
}

export async function getVehicleBrandPage(params = {}, requester) {
  return requestWithDualRoute("/api/v1/vehicle-brands/admin-list", "/api/vehicle-brands/admin-list", {
    method: "GET",
    params: { page: 1, pageSize: 20, ...params },
  }, requester);
}

export async function createVehicleBrand(payload, requester) {
  return requestWithDualRoute("/api/v1/vehicle-brands", "/api/vehicle-brands", {
    method: "POST",
    data: payload,
  }, requester);
}

export async function updateVehicleBrand(id, payload, requester) {
  return requestWithDualRoute(`/api/v1/vehicle-brands/${id}`, `/api/vehicle-brands/${id}`, {
    method: "PUT",
    data: payload,
  }, requester);
}

export async function deleteVehicleBrand(id, requester) {
  await requestWithDualRoute(`/api/v1/vehicle-brands/${id}`, `/api/vehicle-brands/${id}`, {
    method: "DELETE",
  }, requester);
  return true;
}

export async function getAllVehicleModels(params = {}, requester) {
  return requestWithDualRoute("/api/v1/vehicle-models", "/api/vehicle-models", {
    method: "GET",
    params: { page: 1, pageSize: 200, ...params },
  }, requester);
}

export async function getVehicleModelPage(params = {}, requester) {
  return requestWithDualRoute("/api/v1/vehicle-models/admin-list", "/api/vehicle-models/admin-list", {
    method: "GET",
    params: { page: 1, pageSize: 20, ...params },
  }, requester);
}

export async function createVehicleModel(payload, requester) {
  return requestWithDualRoute("/api/v1/vehicle-models", "/api/vehicle-models", {
    method: "POST",
    data: payload,
  }, requester);
}

export async function updateVehicleModel(id, payload, requester) {
  return requestWithDualRoute(`/api/v1/vehicle-models/${id}`, `/api/vehicle-models/${id}`, {
    method: "PUT",
    data: payload,
  }, requester);
}

export async function deleteVehicleModel(id, requester) {
  await requestWithDualRoute(`/api/v1/vehicle-models/${id}`, `/api/vehicle-models/${id}`, {
    method: "DELETE",
  }, requester);
  return true;
}
