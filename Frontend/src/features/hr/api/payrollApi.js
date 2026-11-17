export function buildBoundedPaginationParams(params = {}, defaultPageSize = 10, maxPageSize = 100) {
  const pageNumber = Number(params.page);
  const safePage = Number.isFinite(pageNumber) && pageNumber > 0 ? Math.trunc(pageNumber) : 1;

  const sizeNumber = Number(params.pageSize);
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

export async function getPayrollRuns(params = {}, requester) {
  const boundedParams = buildBoundedPaginationParams(params, 10, 100);
  return requestWithDualRoute(
    "/api/v1/payroll-runs",
    "/api/v1/payroll-runs",
    { method: "GET", params: boundedParams },
    requester
  );
}

export async function getPayrollPreview(params = {}, requester) {
  return requestWithDualRoute(
    "/api/v1/payroll-runs/previews",
    "/api/v1/payroll-runs/previews",
    { method: "GET", params },
    requester
  );
}

export async function createPayrollRun(data = {}, requester) {
  return requestWithDualRoute(
    "/api/v1/payroll-runs",
    "/api/v1/payroll-runs",
    { method: "POST", data },
    requester
  );
}

export async function approvePayrollRun(id, data = {}, requester) {
  return requestWithDualRoute(
    `/api/v1/payroll-runs/${id}/approvals`,
    `/api/v1/payroll-runs/${id}/approvals`,
    { method: "POST", data },
    requester
  );
}
