import { request } from "@app/apiClient";

function toQuery(params) {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") return;
    search.set(key, String(value));
  });
  const query = search.toString();
  return query ? `?${query}` : "";
}

export function getCustomerConversations(params = {}) {
  return request({
    url: `/api/chat/my-conversations${toQuery(params)}`,
    method: "GET",
  });
}

export function getCustomerConversation(conversationId) {
  return request({
    url: `/api/chat/conversations/${conversationId}`,
    method: "GET",
  });
}

export function getCustomerMessengerConversation() {
  return request({
    url: "/api/chat/messenger/conversation",
    method: "GET",
  });
}

export function getCustomerMessages(conversationId, params = {}) {
  return request({
    url: `/api/chat/conversations/${conversationId}/messages${toQuery(params)}`,
    method: "GET",
  });
}

export function getCustomerConversationOrders(conversationId) {
  return request({
    url: `/api/chat/conversations/${conversationId}/orders`,
    method: "GET",
  });
}

export function createCustomerConversation(payload) {
  return request({
    url: "/api/chat/conversations",
    method: "POST",
    data: payload,
  });
}

export function sendCustomerMessage(conversationId, payload) {
  return request({
    url: `/api/chat/conversations/${conversationId}/messages`,
    method: "POST",
    data: payload,
  });
}

export function uploadCustomerChatAttachments(conversationId, files) {
  const formData = new FormData();
  (files || []).forEach((file) => formData.append("files", file));

  return request({
    url: `/api/chat/conversations/${conversationId}/attachments`,
    method: "POST",
    data: formData,
    headers: {
      "Content-Type": "multipart/form-data",
    },
  });
}

export function markCustomerRead(conversationId, payload) {
  return request({
    url: `/api/chat/conversations/${conversationId}/read-state`,
    method: "PUT",
    data: payload,
  });
}

export function getReceptionConversations(params = {}) {
  return request({
    url: `/api/reception/chat/conversations${toQuery(params)}`,
    method: "GET",
  });
}

export function getReceptionConversation(conversationId) {
  return request({
    url: `/api/reception/chat/conversations/${conversationId}`,
    method: "GET",
  });
}

export function getReceptionMessages(conversationId, params = {}) {
  return request({
    url: `/api/reception/chat/conversations/${conversationId}/messages${toQuery(params)}`,
    method: "GET",
  });
}

export function getReceptionConversationOrders(conversationId) {
  return request({
    url: `/api/reception/chat/conversations/${conversationId}/orders`,
    method: "GET",
  });
}

export function sendReceptionMessage(conversationId, payload) {
  return request({
    url: `/api/reception/chat/conversations/${conversationId}/messages`,
    method: "POST",
    data: payload,
  });
}

export function uploadReceptionChatAttachments(conversationId, files) {
  const formData = new FormData();
  (files || []).forEach((file) => formData.append("files", file));

  return request({
    url: `/api/reception/chat/conversations/${conversationId}/attachments`,
    method: "POST",
    data: formData,
    headers: {
      "Content-Type": "multipart/form-data",
    },
  });
}

export function updateReceptionConversationStatus(conversationId, payload) {
  return request({
    url: `/api/reception/chat/conversations/${conversationId}/status`,
    method: "PUT",
    data: payload,
  });
}

export function assignReceptionConversation(conversationId, payload) {
  return request({
    url: `/api/reception/chat/conversations/${conversationId}/assign`,
    method: "PUT",
    data: payload,
  });
}

export function markReceptionRead(conversationId, payload) {
  return request({
    url: `/api/reception/chat/conversations/${conversationId}/read-state`,
    method: "PUT",
    data: payload,
  });
}
