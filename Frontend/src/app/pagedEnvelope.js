/**
 * Shared module for JSON Paged Envelope (Section 2) & RESTful /api/v1/... Dual-Route (Section 3)
 * standardization across MyGarage Frontend.
 */

export function buildBoundedPaginationParams(params = {}, defaultPageSize = 25, maxPageSize = 200) {
  const safeDefaultSize =
    Number.isFinite(Number(defaultPageSize)) && Number(defaultPageSize) > 0
      ? Math.trunc(Number(defaultPageSize))
      : 25;
  const safeMaxSize =
    Number.isFinite(Number(maxPageSize)) && Number(maxPageSize) > 0
      ? Math.trunc(Number(maxPageSize))
      : 200;

  const cleaned = {};
  if (params && typeof params === "object" && !Array.isArray(params)) {
    for (const [key, value] of Object.entries(params)) {
      if (value !== "" && value !== null && value !== undefined) {
        cleaned[key] = value;
      }
    }
  }

  const pageNumber = Number(params?.page);
  const safePage = Number.isFinite(pageNumber) && pageNumber > 0 ? Math.trunc(pageNumber) : 1;

  const sizeNumber = Number(params?.pageSize);
  const requestedSize =
    Number.isFinite(sizeNumber) && sizeNumber > 0 ? Math.trunc(sizeNumber) : safeDefaultSize;
  const safePageSize = Math.min(Math.max(1, requestedSize), safeMaxSize);

  return {
    ...cleaned,
    page: safePage,
    pageSize: safePageSize,
  };
}

export function normalizePagedEnvelope(payload, fallbackPage = 1, fallbackPageSize = 25) {
  const safeFallbackPage =
    Number.isFinite(Number(fallbackPage)) && Number(fallbackPage) > 0
      ? Math.trunc(Number(fallbackPage))
      : 1;
  const safeFallbackPageSize =
    Number.isFinite(Number(fallbackPageSize)) && Number(fallbackPageSize) > 0
      ? Math.trunc(Number(fallbackPageSize))
      : 25;

  if (Array.isArray(payload)) {
    const items = payload;
    const totalCount = items.length;
    const pageSize = Math.max(1, totalCount > 0 ? Math.max(safeFallbackPageSize, totalCount) : safeFallbackPageSize);
    const totalPages = totalCount > 0 ? Math.ceil(totalCount / pageSize) : 0;
    return {
      items,
      totalCount,
      total: totalCount,
      page: safeFallbackPage,
      pageSize,
      totalPages,
      hasPreviousPage: safeFallbackPage > 1,
      hasNextPage: safeFallbackPage < totalPages,
    };
  }

  if (!payload || typeof payload !== "object") {
    return {
      items: [],
      totalCount: 0,
      total: 0,
      page: safeFallbackPage,
      pageSize: safeFallbackPageSize,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
    };
  }

  // Extract items array from standard envelope, data envelope, or OData v4 envelope
  let items = [];
  if (Array.isArray(payload.items)) {
    items = payload.items;
  } else if (Array.isArray(payload.data)) {
    items = payload.data;
  } else if (Array.isArray(payload.value)) {
    items = payload.value;
  }

  // Extract totalCount from standard totalCount, total, or OData @odata.count
  const rawTotal =
    payload.totalCount ??
    payload.total ??
    payload["@odata.count"] ??
    items.length;
  const parsedTotal = Number(rawTotal);
  const totalCount =
    Number.isFinite(parsedTotal) && parsedTotal >= 0
      ? Math.max(items.length, Math.trunc(parsedTotal))
      : items.length;

  const parsedPage = Number(payload.page ?? payload.currentPage ?? safeFallbackPage);
  const page = Number.isFinite(parsedPage) && parsedPage > 0 ? Math.trunc(parsedPage) : safeFallbackPage;

  const parsedPageSize = Number(payload.pageSize ?? safeFallbackPageSize);
  const pageSize =
    Number.isFinite(parsedPageSize) && parsedPageSize > 0
      ? Math.trunc(parsedPageSize)
      : safeFallbackPageSize;

  const computedTotalPages = totalCount > 0 ? Math.ceil(totalCount / pageSize) : 0;
  const parsedTotalPages = Number(payload.totalPages);
  const totalPages =
    Number.isFinite(parsedTotalPages) && parsedTotalPages >= 0
      ? Math.trunc(parsedTotalPages)
      : computedTotalPages;

  const hasPreviousPage =
    typeof payload.hasPreviousPage === "boolean"
      ? payload.hasPreviousPage
      : page > 1;
  const hasNextPage =
    typeof payload.hasNextPage === "boolean"
      ? payload.hasNextPage
      : page < totalPages;

  return {
    items,
    totalCount,
    total: totalCount,
    page,
    pageSize,
    totalPages,
    hasPreviousPage,
    hasNextPage,
  };
}

async function getRequester(requester) {
  if (typeof requester === "function") return requester;
  const client = await import("@app/apiClient");
  return client.request;
}

/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route only when 404 is encountered.
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
