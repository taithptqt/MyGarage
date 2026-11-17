import axios from "axios";
import { collectPages } from "./collectPages";

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

function decodeUnicodeEscapes(text) {
  const raw = String(text ?? "");
  if (!raw || !/\\u[0-9a-fA-F]{4}/.test(raw)) return raw;
  return raw.replace(/\\u([0-9a-fA-F]{4})/g, (_, code) =>
    String.fromCharCode(parseInt(code, 16))
  );
}

function normalizeErrorText(text) {
  return decodeUnicodeEscapes(String(text ?? "")).trim();
}

function extractErrorMessage(error) {
  const data = error?.response?.data;
  if (!data) return normalizeErrorText(error?.message) || "Request failed.";
  if (typeof data === "string" && data.trim()) return normalizeErrorText(data);
  if (data?.message) return normalizeErrorText(data.message);
  // Handle ASP.NET Core validation errors
  if (data?.errors) {
    const errorDetails = normalizeErrorText(Object.values(data.errors).flat().join(" "));
    const title = normalizeErrorText(data.title);
    return title ? `${title}: ${errorDetails}` : errorDetails;
  }
  if (data?.title) return normalizeErrorText(data.title);
  if (error?.message) return normalizeErrorText(error.message);
  return "Request failed.";
}

function isPublicAuthRequestUrl(requestUrl) {
  const normalizedUrl = String(requestUrl || "")
    .toLowerCase()
    .replace(/\/api\/(?!v1\/)/, "/api/v1/");
  return [
    "/api/v1/auth/session",
    "/api/v1/auth/sessions",
    "/api/v1/auth/accounts",
    "/api/v1/auth/external-sessions/google",
    "/api/v1/auth/credential-recovery-requests",
    "/api/v1/auth/credential-recovery-finalizations",
  ].some((route) => normalizedUrl.includes(route));
}

const apiClient = axios.create({
  baseURL: API_BASE_URL || undefined,
  withCredentials: true,
  timeout: 15000,
  headers: {
    "Content-Type": "application/json"
  }
});

const UNSAFE_HTTP_METHODS = new Set(["post", "put", "patch", "delete"]);
const INFLIGHT_REQUESTS = new Map();
const REQUEST_COOLDOWN = new Map();
const REQUEST_COOLDOWN_MS = 800;

function isPlainObject(value) {
  return Object.prototype.toString.call(value) === "[object Object]";
}

function stableSerialize(value) {
  if (value === null || value === undefined) return "";
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  if (value instanceof Date) return value.toISOString();
  if (Array.isArray(value)) return `[${value.map((item) => stableSerialize(item)).join(",")}]`;

  if (typeof FormData !== "undefined" && value instanceof FormData) {
    const pairs = [];
    value.forEach((entryValue, entryKey) => {
      if (typeof entryValue === "string") {
        pairs.push([entryKey, entryValue]);
        return;
      }

      const fileName = entryValue?.name || "blob";
      const fileSize = Number(entryValue?.size || 0);
      pairs.push([entryKey, `${fileName}:${fileSize}`]);
    });
    pairs.sort(([a], [b]) => String(a).localeCompare(String(b)));
    return JSON.stringify(pairs);
  }

  if (isPlainObject(value)) {
    const keys = Object.keys(value).sort();
    return `{${keys.map((key) => `${JSON.stringify(key)}:${stableSerialize(value[key])}`).join(",")}}`;
  }

  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

function normalizeMethod(method) {
  return String(method || "get").trim().toLowerCase();
}

function normalizeUrl(config) {
  const base = String(config?.baseURL || API_BASE_URL || "").replace(/\/$/, "");
  const path = String(config?.url || "").trim();
  if (!path) return base;
  if (/^https?:\/\//i.test(path)) return path;
  if (!base) return path;
  return `${base}${path.startsWith("/") ? "" : "/"}${path}`;
}

function getRequestFingerprint(config) {
  const method = normalizeMethod(config?.method);
  const url = normalizeUrl(config);
  const params = stableSerialize(config?.params);
  const data = stableSerialize(config?.data);
  return `${method}|${url}|${params}|${data}`;
}

function pruneCooldown(now = Date.now()) {
  for (const [fingerprint, expiry] of REQUEST_COOLDOWN.entries()) {
    if (!expiry || expiry <= now) REQUEST_COOLDOWN.delete(fingerprint);
  }
}

function markRequestDone(fingerprint) {
  if (!fingerprint) return;
  INFLIGHT_REQUESTS.delete(fingerprint);
  REQUEST_COOLDOWN.set(fingerprint, Date.now() + REQUEST_COOLDOWN_MS);
}

apiClient.interceptors.request.use((config) => {
  const method = normalizeMethod(config.method);
  const isFormDataRequest =
    typeof FormData !== "undefined" && config.data instanceof FormData;

  // Do not dedupe multipart uploads on client side; file uploads can legitimately
  // have similar metadata and should be guarded by server-side checks.
  // Also bypass dedupe if caller explicitly sets skipDuplicateGuard or skipSpamGuard.
  if (UNSAFE_HTTP_METHODS.has(method) && !isFormDataRequest && !config.skipDuplicateGuard && !config.skipSpamGuard) {
    const fingerprint = getRequestFingerprint(config);
    const now = Date.now();
    pruneCooldown(now);

    if (INFLIGHT_REQUESTS.has(fingerprint)) {
      return Promise.reject(new Error("Yeu cau dang duoc xu ly. Vui long cho."));
    }

    const cooldownUntil = REQUEST_COOLDOWN.get(fingerprint) || 0;
    if (cooldownUntil > now) {
      return Promise.reject(new Error("Thao tac trung lap qua nhanh. Vui long thu lai sau."));
    }

    INFLIGHT_REQUESTS.set(fingerprint, now);
    config.__spamFingerprint = fingerprint;
  }

  // FormData cần boundary tự sinh; header json mặc định làm ASP.NET không bind IFormFile → 400
  if (isFormDataRequest) {
    if (config.headers && typeof config.headers.delete === "function") {
      config.headers.delete("Content-Type");
    } else {
      delete config.headers["Content-Type"];
    }
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => {
    markRequestDone(response?.config?.__spamFingerprint);
    return response;
  },
  (error) => {
    markRequestDone(error?.config?.__spamFingerprint);
    if (error.response?.status === 401) {
      localStorage.removeItem("mygarage_token");
      localStorage.removeItem("mygarage_user");
      localStorage.removeItem("mygarage_last_active_at");
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
    return await collectPages(config, response, (nextConfig) => apiClient.request(nextConfig));
  } catch (error) {
    const wrapped = new Error(extractErrorMessage(error));
    wrapped.response = error?.response;
    wrapped.status = error?.response?.status;
    wrapped.data = error?.response?.data;
    throw wrapped;
  }
}

export async function requestSafe(config) {
  try {
    const response = await apiClient.request(config);
    const data = await collectPages(config, response, (nextConfig) => apiClient.request(nextConfig));
    return { ok: true, status: response.status, data, headers: response.headers };
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
