export function buildBoundedPaginationParams(params = {}, defaultPageSize = 25, maxPageSize = 200) {
  const pageNumber = Number(params?.page);
  const safePage = Number.isFinite(pageNumber) && pageNumber > 0 ? Math.trunc(pageNumber) : 1;

  const sizeNumber = Number(params?.pageSize);
  const requestedSize = Number.isFinite(sizeNumber) && sizeNumber > 0 ? Math.trunc(sizeNumber) : defaultPageSize;
  const safePageSize = Math.min(Math.max(1, requestedSize), maxPageSize);

  return {
    ...params,
    page: safePage,
    pageSize: safePageSize,
  };
}

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

export async function getGatePasses(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const rawParams = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const boundedParams = buildBoundedPaginationParams(rawParams, 25, 200);

  return requestWithDualRoute(
    "/api/v1/gate-passes",
    "/api/gate-passes",
    {
      method: "GET",
      params: boundedParams,
    },
    requester
  );
}

export async function getGatePassById(id, requester) {
  return requestWithDualRoute(
    `/api/v1/gate-passes/${id}`,
    `/api/gate-passes/${id}`,
    {
      method: "GET",
    },
    requester
  );
}

export async function createGatePass(payload, requester) {
  return requestWithDualRoute(
    "/api/v1/gate-passes",
    "/api/gate-passes",
    {
      method: "POST",
      data: payload,
    },
    requester
  );
}

export async function updateGatePass(id, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/gate-passes/${id}`,
    `/api/gate-passes/${id}`,
    {
      method: "PUT",
      data: payload,
    },
    requester
  );
}

export async function deleteGatePass(id, requester) {
  return requestWithDualRoute(
    `/api/v1/gate-passes/${id}`,
    `/api/gate-passes/${id}`,
    {
      method: "DELETE",
    },
    requester
  );
}

export async function getApprovalRequests(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const rawParams = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const boundedParams = buildBoundedPaginationParams(rawParams, 25, 200);

  return requestWithDualRoute(
    "/api/v1/approval-requests",
    "/api/approval-requests",
    {
      method: "GET",
      params: boundedParams,
    },
    requester
  );
}

export async function getApprovalRequestById(id, requester) {
  return requestWithDualRoute(
    `/api/v1/approval-requests/${id}`,
    `/api/approval-requests/${id}`,
    {
      method: "GET",
    },
    requester
  );
}

export async function createApprovalRequest(payload, requester) {
  return requestWithDualRoute(
    "/api/v1/approval-requests",
    "/api/approval-requests",
    {
      method: "POST",
      data: payload,
    },
    requester
  );
}

export async function updateApprovalRequest(id, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/approval-requests/${id}`,
    `/api/approval-requests/${id}`,
    {
      method: "PUT",
      data: payload,
    },
    requester
  );
}
