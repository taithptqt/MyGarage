import { getApiBaseUrl as getBaseUrl, requestSafe } from "../../../app/apiClient";

export async function login(payload) {
  return requestSafe({
    url: "/api/Auth/login",
    method: "POST",
    data: payload
  });
}

export async function register(payload) {
  return requestSafe({
    url: "/api/Auth/register",
    method: "POST",
    data: payload
  });
}

export async function googleLogin(payload) {
  return requestSafe({
    url: "/api/Auth/google",
    method: "POST",
    data: typeof payload === "string" ? { idToken: payload } : payload
  });
}

export async function getProfile() {
  return requestSafe({
    url: "/api/Auth/me",
    method: "GET"
  });
}

export function getApiBaseUrl() {
  return getBaseUrl();
}
