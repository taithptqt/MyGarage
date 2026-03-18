function resolveApiBaseUrl() {
  if (typeof window !== "undefined") {
    const isLocalhost =
      window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1";

    if (isLocalhost) {
      if (window.location.protocol === "https:" && window.location.port === "7009") {
        return "";
      }
      return "https://localhost:7009";
    }
  }

  const configured = (import.meta.env.VITE_API_BASE_URL || "").trim();
  if (configured) {
    return configured.replace(/\/$/, "");
  }
  return "";
}

const API_BASE_URL = resolveApiBaseUrl();

export async function login(payload) {
  return postJson("/api/Auth/login", payload);
}

export async function register(payload) {
  return postJson("/api/Auth/register", payload);
}

export async function googleLogin(idToken) {
  return postJson("/api/Auth/google", { idToken });
}

export function getApiBaseUrl() {
  return API_BASE_URL || window.location.origin;
}

async function postJson(path, payload) {
  const url = API_BASE_URL ? `${API_BASE_URL}${path}` : path;

  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  const data = await readResponseBody(response);
  return { ok: response.ok, data };
}

async function readResponseBody(response) {
  const contentType = response.headers.get("content-type") || "";
  if (contentType.includes("application/json")) {
    return await response.json();
  }

  const text = await response.text();
  return { message: text };
}
