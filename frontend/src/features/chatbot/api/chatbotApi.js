import { request } from "@app/apiClient";

export function createChatbotSession(payload = {}) {
  return request({
    url: "/api/chatbot/sessions",
    method: "POST",
    data: payload,
  });
}

export function orchestrateChatbot(payload) {
  return request({
    url: "/api/chatbot/orchestrate",
    method: "POST",
    data: payload,
  });
}

export function getChatbotSession(sessionId) {
  return request({
    url: `/api/chatbot/sessions/${sessionId}`,
    method: "GET",
  });
}
