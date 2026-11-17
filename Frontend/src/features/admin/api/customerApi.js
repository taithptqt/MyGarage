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

export async function getAllCustomers(branchId, params = {}, requester) {
  let mergedParams = { page: 1, pageSize: 100 };
  if (typeof branchId === "object" && branchId !== null) {
    mergedParams = { ...mergedParams, ...branchId };
  } else {
    if (branchId) mergedParams.branchId = branchId;
    mergedParams = { ...mergedParams, ...params };
  }
  return requestWithDualRoute("/api/v1/customers", "/api/v1/customers", {
    method: "GET",
    params: mergedParams,
  }, requester);
}

export async function getReceptionCustomerVehicleReferences(branchId, requester) {
  const send = await getRequester(requester);
  const params = branchId ? { branchId } : {};
  return Promise.all([
    send({ url: "/api/v1/customers", method: "GET", params }),
    send({ url: "/api/v1/vehicles", method: "GET" }),
    send({ url: "/api/v1/customer-vehicles", method: "GET" }),
  ]);
}

export async function getReceptionOrderReferences(branchId, requester) {
  const send = await getRequester(requester);
  return Promise.all([
    send({ url: "/api/v1/customers", method: "GET", params: branchId ? { branchId } : {} }),
    send({ url: "/api/v1/vehicles", method: "GET" }).catch(() => []),
  ]);
}

export async function getCustomerPage(params = {}, requester) {
  return requestWithDualRoute("/api/v1/customers/admin-list", "/api/v1/customers/admin-list", {
    method: "GET",
    params: { page: 1, pageSize: 20, ...params },
  }, requester);
}

export async function getCustomerById(id, requester) {
  return requestWithDualRoute(`/api/v1/customers/${id}`, `/api/v1/customers/${id}`, {
    method: "GET",
  }, requester);
}

export async function createCustomer(payload, requester) {
  return requestWithDualRoute("/api/v1/customers", "/api/v1/customers", {
    method: "POST",
    data: payload,
  }, requester);
}

export async function updateCustomer(id, payload, requester) {
  return requestWithDualRoute(`/api/v1/customers/${id}`, `/api/v1/customers/${id}`, {
    method: "PUT",
    data: payload,
  }, requester);
}

export async function deleteCustomer(id, requester) {
  await requestWithDualRoute(`/api/v1/customers/${id}`, `/api/v1/customers/${id}`, {
    method: "DELETE",
  }, requester);
  return true;
}

export async function getAllCustomerTiers(params = {}, requester) {
  return requestWithDualRoute("/api/v1/customer-tiers", "/api/v1/customer-tiers", {
    method: "GET",
    params: { page: 1, pageSize: 100, ...params },
  }, requester);
}

export async function getAllCustomerVehicles(params = {}, requester) {
  return requestWithDualRoute("/api/v1/customer-vehicles", "/api/v1/customer-vehicles", {
    method: "GET",
    params: { page: 1, pageSize: 100, ...params },
  }, requester);
}

export async function createCustomerVehicle(payload, requester) {
  return requestWithDualRoute("/api/v1/customer-vehicles", "/api/v1/customer-vehicles", {
    method: "POST",
    data: payload,
  }, requester);
}

export async function updateCustomerVehicle(id, payload, requester) {
  return requestWithDualRoute(`/api/v1/customer-vehicles/${id}`, `/api/v1/customer-vehicles/${id}`, {
    method: "PUT",
    data: payload,
  }, requester);
}

export async function deleteCustomerVehicle(id, requester) {
  await requestWithDualRoute(`/api/v1/customer-vehicles/${id}`, `/api/v1/customer-vehicles/${id}`, {
    method: "DELETE",
  }, requester);
  return true;
}

export async function getAllVehicles(params = {}, requester) {
  return requestWithDualRoute("/api/v1/vehicles", "/api/v1/vehicles", {
    method: "GET",
    params: { page: 1, pageSize: 100, ...params },
  }, requester);
}

export async function getVehicleById(id, requester) {
  return requestWithDualRoute(`/api/v1/vehicles/${id}`, `/api/v1/vehicles/${id}`, {
    method: "GET",
  }, requester);
}

export async function createVehicle(payload, requester) {
  return requestWithDualRoute("/api/v1/vehicles", "/api/v1/vehicles", {
    method: "POST",
    data: payload,
  }, requester);
}

export async function updateVehicle(id, payload, requester) {
  return requestWithDualRoute(`/api/v1/vehicles/${id}`, `/api/v1/vehicles/${id}`, {
    method: "PUT",
    data: payload,
  }, requester);
}
