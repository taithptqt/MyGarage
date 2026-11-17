export function buildBoundedPaginationParams(params = {}, defaultPageSize = 20, maxPageSize = 100) {
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

export function getAiForecasts(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const rawParams = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const boundedParams = buildBoundedPaginationParams(rawParams, 20, 100);

  return requestWithDualRoute(
    "/api/v1/ai-forecasts",
    "/api/v1/ai-forecasts",
    {
      method: "GET",
      params: boundedParams,
    },
    requester
  );
}

export function getAiKnowledgePage(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const rawParams = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const boundedParams = buildBoundedPaginationParams(rawParams, 20, 100);

  return requestWithDualRoute(
    "/api/v1/ai-knowledge-bases/page",
    "/api/v1/ai-knowledge-bases/page",
    {
      method: "GET",
      params: boundedParams,
    },
    requester
  );
}

export function createAiKnowledgeEntry(payload, requester) {
  return requestWithDualRoute(
    "/api/v1/ai-knowledge-bases",
    "/api/v1/ai-knowledge-bases",
    {
      method: "POST",
      data: payload,
    },
    requester
  );
}

export function updateAiKnowledgeEntry(id, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/ai-knowledge-bases/${id}`,
    `/api/v1/ai-knowledge-bases/${id}`,
    {
      method: "PUT",
      data: payload,
    },
    requester
  );
}

export function deleteAiKnowledgeEntry(id, requester) {
  return requestWithDualRoute(
    `/api/v1/ai-knowledge-bases/${id}`,
    `/api/v1/ai-knowledge-bases/${id}`,
    {
      method: "DELETE",
    },
    requester
  );
}

export function getAiModelConfigPage(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const rawParams = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const boundedParams = buildBoundedPaginationParams(rawParams, 20, 100);

  return requestWithDualRoute(
    "/api/v1/ai-model-configs/page",
    "/api/v1/ai-model-configs/page",
    {
      method: "GET",
      params: boundedParams,
    },
    requester
  );
}

export function createAiModelConfig(payload, requester) {
  return requestWithDualRoute(
    "/api/v1/ai-model-configs",
    "/api/v1/ai-model-configs",
    {
      method: "POST",
      data: payload,
    },
    requester
  );
}

export function updateAiModelConfig(id, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/ai-model-configs/${id}`,
    `/api/v1/ai-model-configs/${id}`,
    {
      method: "PUT",
      data: payload,
    },
    requester
  );
}

export function getAiSmartDispatchings(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const rawParams = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const boundedParams = buildBoundedPaginationParams(rawParams, 20, 100);

  return requestWithDualRoute(
    "/api/v1/ai-smart-dispatchings",
    "/api/v1/ai-smart-dispatchings",
    {
      method: "GET",
      params: boundedParams,
    },
    requester
  );
}

export function recommendBayForOrder(serviceOrderId, requester) {
  return requestWithDualRoute(
    `/api/v1/ai-smart-dispatchings/recommend-bay/${serviceOrderId}`,
    `/api/v1/ai-smart-dispatchings/recommend-bay/${serviceOrderId}`,
    {
      method: "GET",
    },
    requester
  );
}
