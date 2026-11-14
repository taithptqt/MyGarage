const ENVELOPE_KEYS = new Set([
  "items",
  "totalCount",
  "total",
  "page",
  "pageSize",
  "totalPages",
  "hasPreviousPage",
  "hasNextPage",
]);

function parseSafeInt(value) {
  if (typeof value === "number") {
    return Number.isSafeInteger(value) ? value : NaN;
  }
  if (typeof value === "string" && /^\d+$/.test(value)) {
    const parsed = Number(value);
    return Number.isSafeInteger(parsed) ? parsed : NaN;
  }
  return NaN;
}

// Preserve legacy callers that expect a complete array while allowing explicit paging.
export async function collectPages(config, response, send) {
  const method = String(config?.method || "get").toLowerCase();
  if (method !== "get") return response?.data;

  const params = config?.params || {};
  const query = String(config?.url || "");
  const searchParams = typeof URLSearchParams !== "undefined" && params instanceof URLSearchParams;
  const entries = searchParams ? Array.from(params.entries()) : Object.entries(params);
  const hasPaging =
    /[?&](page|pageSize)=/i.test(query) ||
    entries.some(([key, value]) => /^(page|pageSize)$/i.test(key) && value != null);
  const isDedicatedPageEndpoint =
    /\/(page|admin-list|retail-catalog|reception-list|notifications|admin-changes|catalog)(\/|$|\?)/i.test(query);

  if (hasPaging || isDedicatedPageEndpoint) return response?.data;

  const data = response?.data;
  if (Array.isArray(data)) return data;
  if (!data || typeof data !== "object" || !Array.isArray(data.items)) return data;
  if (Object.keys(data).some((key) => !ENVELOPE_KEYS.has(key))) return data;

  const firstPage = parseSafeInt(data.page);
  const pageSize = parseSafeInt(data.pageSize);
  const totalCount = parseSafeInt(data.totalCount ?? data.total);
  const totalPages = parseSafeInt(data.totalPages);
  const hasMismatchedTotals =
    data.totalCount != null && data.total != null && parseSafeInt(data.totalCount) !== parseSafeInt(data.total);
  const expectedTotalPages = totalCount === 0 ? 0 : Math.ceil(totalCount / pageSize);

  if (
    hasMismatchedTotals ||
    ![firstPage, pageSize, totalPages, totalCount].every(Number.isSafeInteger) ||
    firstPage !== 1 ||
    pageSize <= 0 ||
    totalCount < 0 ||
    totalPages !== expectedTotalPages ||
    data.items.length !== Math.min(totalCount, pageSize)
  ) {
    throw new Error("Invalid or changed paginated response.");
  }

  if (totalPages <= 1) return [...data.items];

  const items = [...data.items];
  for (let page = 2; page <= totalPages; page += 1) {
    const nextParams = searchParams ? new URLSearchParams(params) : { ...params, page, pageSize };
    if (searchParams) {
      nextParams.set("page", String(page));
      nextParams.set("pageSize", String(pageSize));
    }
    const next = await send({ ...config, params: nextParams });
    const nextData = next?.data;
    const expectedLength = Math.min(pageSize, totalCount - (page - 1) * pageSize);
    const nextTotalCount = parseSafeInt(nextData?.totalCount ?? nextData?.total);
    const nextHasMismatchedTotals =
      nextData?.totalCount != null &&
      nextData?.total != null &&
      parseSafeInt(nextData.totalCount) !== parseSafeInt(nextData.total);

    if (
      nextHasMismatchedTotals ||
      !nextData ||
      typeof nextData !== "object" ||
      !Array.isArray(nextData.items) ||
      nextData.items.length !== expectedLength ||
      parseSafeInt(nextData.page) !== page ||
      parseSafeInt(nextData.pageSize) !== pageSize ||
      parseSafeInt(nextData.totalPages) !== totalPages ||
      nextTotalCount !== totalCount
    ) {
      throw new Error("Invalid or changed paginated response.");
    }
    items.push(...nextData.items);
  }

  if (items.length !== totalCount) throw new Error("Incomplete paginated response.");
  return items;
}
