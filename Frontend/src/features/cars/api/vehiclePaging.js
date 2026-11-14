/**
 * Ensures pagination parameters are bounded to prevent memory bloat and unbounded loops.
 */
export function buildBoundedPaginationParams(params = {}, defaultPageSize = 20, maxPageSize = 200) {
  const page = Number(params?.page) > 0 ? Number(params.page) : 1;
  const rawSize = Number(params?.pageSize) > 0 ? Number(params.pageSize) : defaultPageSize;
  const pageSize = Math.min(Math.max(1, rawSize), maxPageSize);
  return {
    ...params,
    page,
    pageSize,
  };
}

/**
 * Validates and normalizes vehicle list responses.
 */
export function parseVehicleListPage(data) {
  if (!data) return { items: [], total: 0 };
  const items = Array.isArray(data.items) ? data.items : Array.isArray(data) ? data : [];
  const total = Number.isSafeInteger(Number(data.total)) ? Number(data.total) : items.length;
  return { items, total };
}
