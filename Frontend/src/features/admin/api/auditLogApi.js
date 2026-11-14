export function buildBoundedPaginationParams(params = {}, defaultPageSize = 50, maxPageSize = 200) {
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

function cleanParams(params = {}) {
  return Object.fromEntries(
    Object.entries(params || {}).filter(([, value]) => value !== "" && value !== null && value !== undefined)
  );
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

export async function getAdminChangeLogs(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const rawParams = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const boundedParams = buildBoundedPaginationParams(cleanParams(rawParams), 50, 200);

  return requestWithDualRoute(
    "/api/v1/audit-logs/admin-changes",
    "/api/audit-logs/admin-changes",
    {
      method: "GET",
      params: boundedParams,
    },
    requester
  );
}

export async function getAuditLogs(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const rawParams = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const boundedParams = buildBoundedPaginationParams(cleanParams(rawParams), 50, 200);

  return requestWithDualRoute(
    "/api/v1/audit-logs",
    "/api/audit-logs",
    {
      method: "GET",
      params: boundedParams,
    },
    requester
  );
}

export async function getAuditLogById(id, requester) {
  return requestWithDualRoute(
    `/api/v1/audit-logs/${id}`,
    `/api/audit-logs/${id}`,
    {
      method: "GET",
    },
    requester
  );
}
