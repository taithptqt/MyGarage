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

function toQuery(params) {
  if (!params || typeof params !== "object") return "";
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") return;
    search.set(key, String(value));
  });
  const query = search.toString();
  return query ? `?${query}` : "";
}

export function getCustomerConversations(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const params = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const query = toQuery(params);
  return requestWithDualRoute(
    `/api/v1/chat/conversations${query}`,
    `/api/v1/chat/my-conversations${query}`,
    { method: "GET" },
    requester
  );
}

export function getCustomerConversation(conversationId, requester) {
  return requestWithDualRoute(
    `/api/v1/chat/conversations/${conversationId}`,
    `/api/v1/chat/conversations/${conversationId}`,
    { method: "GET" },
    requester
  );
}

export function getCustomerMessengerConversation(requester) {
  return requestWithDualRoute(
    "/api/v1/chat/messenger/conversation",
    "/api/v1/chat/messenger/conversation",
    { method: "GET" },
    requester
  );
}

export function getCustomerMessages(conversationId, paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const params = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const query = toQuery(params);
  return requestWithDualRoute(
    `/api/v1/chat/conversations/${conversationId}/messages${query}`,
    `/api/v1/chat/conversations/${conversationId}/messages${query}`,
    { method: "GET" },
    requester
  );
}

export function getCustomerConversationOrders(conversationId, requester) {
  return requestWithDualRoute(
    `/api/v1/chat/conversations/${conversationId}/orders`,
    `/api/v1/chat/conversations/${conversationId}/orders`,
    { method: "GET" },
    requester
  );
}

export function createCustomerConversation(payload, requester) {
  return requestWithDualRoute(
    "/api/v1/chat/conversations",
    "/api/v1/chat/conversations",
    { method: "POST", data: payload },
    requester
  );
}

export function sendCustomerMessage(conversationId, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/chat/conversations/${conversationId}/messages`,
    `/api/v1/chat/conversations/${conversationId}/messages`,
    { method: "POST", data: payload },
    requester
  );
}

export function uploadCustomerChatAttachments(conversationId, files, requester) {
  const formData = new FormData();
  (files || []).forEach((file) => formData.append("files", file));

  return requestWithDualRoute(
    `/api/v1/chat/conversations/${conversationId}/attachments`,
    `/api/v1/chat/conversations/${conversationId}/attachments`,
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

export function markCustomerRead(conversationId, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/chat/conversations/${conversationId}/read-state`,
    `/api/v1/chat/conversations/${conversationId}/read-state`,
    { method: "PATCH", data: payload },
    requester
  );
}

export function getReceptionConversations(paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const params = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const query = toQuery(params);
  return requestWithDualRoute(
    `/api/v1/reception/chat/conversations${query}`,
    `/api/v1/reception/chat/conversations${query}`,
    { method: "GET" },
    requester
  );
}

export function getReceptionConversation(conversationId, requester) {
  return requestWithDualRoute(
    `/api/v1/reception/chat/conversations/${conversationId}`,
    `/api/v1/reception/chat/conversations/${conversationId}`,
    { method: "GET" },
    requester
  );
}

export function getReceptionMessages(conversationId, paramsOrRequester = {}, maybeRequester) {
  const isRequester = typeof paramsOrRequester === "function";
  const params = isRequester ? {} : (paramsOrRequester || {});
  const requester = isRequester ? paramsOrRequester : maybeRequester;
  const query = toQuery(params);
  return requestWithDualRoute(
    `/api/v1/reception/chat/conversations/${conversationId}/messages${query}`,
    `/api/v1/reception/chat/conversations/${conversationId}/messages${query}`,
    { method: "GET" },
    requester
  );
}

export function getReceptionConversationOrders(conversationId, requester) {
  return requestWithDualRoute(
    `/api/v1/reception/chat/conversations/${conversationId}/orders`,
    `/api/v1/reception/chat/conversations/${conversationId}/orders`,
    { method: "GET" },
    requester
  );
}

export function sendReceptionMessage(conversationId, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/reception/chat/conversations/${conversationId}/messages`,
    `/api/v1/reception/chat/conversations/${conversationId}/messages`,
    { method: "POST", data: payload },
    requester
  );
}

export function uploadReceptionChatAttachments(conversationId, files, requester) {
  const formData = new FormData();
  (files || []).forEach((file) => formData.append("files", file));

  return requestWithDualRoute(
    `/api/v1/reception/chat/conversations/${conversationId}/attachments`,
    `/api/v1/reception/chat/conversations/${conversationId}/attachments`,
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

export function updateReceptionConversationStatus(conversationId, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/reception/chat/conversations/${conversationId}/status`,
    `/api/v1/reception/chat/conversations/${conversationId}/status`,
    { method: "PATCH", data: payload },
    requester
  );
}

export function assignReceptionConversation(conversationId, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/reception/chat/conversations/${conversationId}/assignments`,
    `/api/v1/reception/chat/conversations/${conversationId}/assignments`,
    { method: "POST", data: payload },
    requester
  );
}

export function markReceptionRead(conversationId, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/reception/chat/conversations/${conversationId}/read-state`,
    `/api/v1/reception/chat/conversations/${conversationId}/read-state`,
    { method: "PATCH", data: payload },
    requester
  );
}
