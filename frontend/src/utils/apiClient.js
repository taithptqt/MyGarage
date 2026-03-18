const API_BASE = (import.meta.env.VITE_API_BASE_URL || "").replace(/\/$/, "");

function getToken() {
  return localStorage.getItem("mygarage_token");
}

function buildUrl(path) {
  return API_BASE ? `${API_BASE}${path}` : path;
}

async function parseResponse(res) {
  const ct = res.headers.get("content-type") || "";
  const body = ct.includes("application/json") ? await res.json() : { message: await res.text() };
  return { ok: res.ok, status: res.status, data: body };
}

function authHeaders(extra = {}) {
  const token = getToken();
  return {
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...extra,
  };
}

export const api = {
  get: (path) =>
    fetch(buildUrl(path), { headers: authHeaders() }).then(parseResponse),

  post: (path, body) =>
    fetch(buildUrl(path), {
      method: "POST",
      headers: authHeaders(),
      body: JSON.stringify(body),
    }).then(parseResponse),

  put: (path, body) =>
    fetch(buildUrl(path), {
      method: "PUT",
      headers: authHeaders(),
      body: JSON.stringify(body),
    }).then(parseResponse),

  delete: (path) =>
    fetch(buildUrl(path), { method: "DELETE", headers: authHeaders() }).then(parseResponse),
};
