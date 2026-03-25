import axios from "axios";

function resolveApiBaseUrl() {
  const configured = (import.meta.env.VITE_API_BASE_URL || "").trim();
  const normalized = configured ? configured.replace(/\/$/, "") : "";

  if (typeof window !== "undefined") {
    const isLocalhost =
      window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1";

    if (isLocalhost) {
      return normalized || "";
    }
  }

  return normalized;
}

export const API_BASE_URL = resolveApiBaseUrl();

export function getApiBaseUrl() {
  if (API_BASE_URL) return API_BASE_URL;
  if (typeof window !== "undefined") return window.location.origin;
  return "";
}

function extractErrorMessage(error) {
  const data = error?.response?.data;
  if (typeof data === "string" && data.trim()) return data;
  if (data?.message) return data.message;
  if (error?.message) return error.message;
  return "Request failed.";
}

const apiClient = axios.create({
  baseURL: API_BASE_URL || undefined,
  headers: {
    "Content-Type": "application/json"
  }
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem("mygarage_token") || localStorage.getItem("token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export async function request(config) {
  try {
    const response = await apiClient.request(config);
    return response.data;
  } catch (error) {
    throw new Error(extractErrorMessage(error));
  }
}

export async function requestSafe(config) {
  try {
    const response = await apiClient.request(config);
    return { ok: true, data: response.data };
  } catch (error) {
    const data = error?.response?.data;
    if (data && typeof data === "object") {
      return { ok: false, data };
    }
    return { ok: false, data: { message: extractErrorMessage(error) } };
  }
}
