function resolveApiBaseUrl() {
  const configured = (import.meta.env.VITE_API_BASE_URL || "").trim();
  if (configured) {
    return configured.replace(/\/$/, "");
  }
  // When empty, use relative paths (same origin as the page).
  // This is the correct default when the backend serves the frontend.
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
