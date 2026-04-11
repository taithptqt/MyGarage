import axios from "axios";

function resolveApiBaseUrl() {
  const configured = (import.meta.env.VITE_API_BASE_URL || "").trim();

  if (typeof window !== "undefined") {
    const isLocalhost =
      window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1";

    if (isLocalhost) {
      // Use relative paths (proxy) for development unless we are already on the backend port
      if (window.location.port !== "7009" && window.location.port !== "5062") {
        return "";
      }
    }
  }

  return configured ? configured.replace(/\/$/, "") : "";
}

export const API_BASE_URL = resolveApiBaseUrl();

export function getApiBaseUrl() {
  if (API_BASE_URL) return API_BASE_URL;
  if (typeof window !== "undefined") return window.location.origin;
  return "";
}

function extractErrorMessage(error) {
  const data = error?.response?.data;
  if (!data) return error?.message || "Request failed.";
  if (typeof data === "string" && data.trim()) return data;
  if (data?.message) return data.message;
  // Handle ASP.NET Core validation errors
  if (data?.errors) {
    const errorDetails = Object.values(data.errors).flat().join(" ");
    return data.title ? `${data.title}: ${errorDetails}` : errorDetails;
  }
  if (data?.title) return data.title;
  if (error?.message) return error.message;
  return "Request failed.";
}

function isPublicAuthRequestUrl(requestUrl) {
  const normalizedUrl = String(requestUrl || "").toLowerCase();
  return [
    "/api/auth/sessions",
    "/api/auth/accounts",
    "/api/auth/external-sessions/google",
    "/api/auth/forgot-password",
    "/api/auth/reset-password",
    "/api/auth/credential-recovery-requests",
    "/api/auth/credential-recovery-finalizations",
  ].some((route) => normalizedUrl.includes(route));
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

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem("mygarage_token");
      localStorage.removeItem("mygarage_user");
      localStorage.removeItem("token");
      
      if (typeof window !== "undefined") {
        const isLoginPage = window.location.pathname === "/login";
        const isPublicAuthRequest = isPublicAuthRequestUrl(error.config?.url);
        
        // Only redirect if NOT on login page AND NOT a login request
        if (!isLoginPage && !isPublicAuthRequest) {
          window.location.href = "/login";
        }
      }
    }
    return Promise.reject(error);
  }
);

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
    return { ok: true, status: response.status, data: response.data };
  } catch (error) {
    const status = error?.response?.status;
    const data = error?.response?.data;
    if (data && typeof data === "object") {
      return { ok: false, status, data };
    }
    return {
      ok: false,
      status,
      data: { message: extractErrorMessage(error) },
    };
  }
}
