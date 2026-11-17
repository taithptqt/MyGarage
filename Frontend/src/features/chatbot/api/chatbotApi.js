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

const V1_BASE = "/api/v1/chatbot";
const LEGACY_BASE = "/api/v1/chatbot";

export function createChatbotSession(payloadOrRequester = {}, maybeRequester) {
  const isRequester = typeof payloadOrRequester === "function";
  const payload = isRequester ? {} : (payloadOrRequester || {});
  const requester = isRequester ? payloadOrRequester : maybeRequester;

  return requestWithDualRoute(
    `${V1_BASE}/sessions`,
    `${LEGACY_BASE}/sessions`,
    {
      method: "POST",
      data: payload,
      skipDuplicateGuard: true,
    },
    requester
  );
}

export function orchestrateChatbot(payload, requester) {
  return requestWithDualRoute(
    `${V1_BASE}/orchestrations`,
    `${LEGACY_BASE}/orchestrations`,
    {
      method: "POST",
      data: payload,
      skipDuplicateGuard: true,
    },
    requester
  );
}

export function getChatbotSession(sessionId, requester) {
  return requestWithDualRoute(
    `${V1_BASE}/sessions/${sessionId}`,
    `${LEGACY_BASE}/sessions/${sessionId}`,
    {
      method: "GET",
    },
    requester
  );
}

export function saveChatbotMessages(payload, requester) {
  return requestWithDualRoute(
    `${V1_BASE}/messages`,
    `${LEGACY_BASE}/messages`,
    {
      method: "POST",
      data: payload,
    },
    requester
  );
}

export function uploadChatbotImage(payloadOrFormData, requester) {
  let formData = payloadOrFormData;
  if (
    typeof FormData !== "undefined" &&
    !(payloadOrFormData instanceof FormData) &&
    payloadOrFormData &&
    typeof payloadOrFormData === "object"
  ) {
    formData = new FormData();
    Object.entries(payloadOrFormData).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        formData.append(key, value);
      }
    });
  }

  return requestWithDualRoute(
    `${V1_BASE}/images`,
    `${LEGACY_BASE}/images`,
    {
      method: "POST",
      data: formData,
      headers: {
        "Content-Type": "multipart/form-data",
      },
    },
    requester
  );
}
