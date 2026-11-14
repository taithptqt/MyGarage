/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route if 404 is encountered.
 */
export async function requestWithDualRoute(v1Url, legacyUrl, options = {}, requester) {
  if (typeof requester !== "function") {
    const client = await import("@app/apiClient");
    requester = client.request;
  }
  try {
    return await requester({ ...options, url: v1Url });
  } catch (err) {
    if ((err?.status === 404 || err?.response?.status === 404) && legacyUrl) {
      return await requester({ ...options, url: legacyUrl });
    }
    throw err;
  }
}

/**
 * Safe version of requestWithDualRoute using requestSafe.
 */
export async function requestSafeWithDualRoute(v1Url, legacyUrl, options = {}, requester) {
  if (typeof requester !== "function") {
    const client = await import("@app/apiClient");
    requester = client.requestSafe;
  }
  const result = await requester({ ...options, url: v1Url });
  if (!result.ok && (result.status === 404 || result.response?.status === 404) && legacyUrl) {
    return await requester({ ...options, url: legacyUrl });
  }
  return result;
}

export function parsePaymentHistoryPage(result) {
  if (!result?.ok) throw new Error(result?.data?.message || "Không tải được lịch sử giao dịch.");
  const data = result.data;
  const isEnvelope = data && typeof data === "object" && !Array.isArray(data) && Array.isArray(data.items);
  const items = isEnvelope ? data.items : data;
  const count = (isEnvelope ? (data.totalCount ?? data.total) : undefined) ?? result.headers?.["x-total-count"];
  const total = Number(count);
  if (count == null || !/^\d+$/.test(String(count)) || !Number.isSafeInteger(total) || total < 0 || !Array.isArray(items) || items.length > total) {
    throw new Error("Không xác định được tổng số giao dịch.");
  }
  return { items, total };
}
