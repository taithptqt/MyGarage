import { requestSafe } from "@app/apiClient";
import { AUTH_ROUTES } from "@features/auth/api/authRoutes";

export async function login(payload) {
  return requestSafe({
    url: AUTH_ROUTES.login,
    method: "POST",
    data: payload,
  });
}

export async function register(payload) {
  return requestSafe({
    url: AUTH_ROUTES.register,
    method: "POST",
    data: payload,
  });
}

export async function googleLogin(payload) {
  return requestSafe({
    url: AUTH_ROUTES.google,
    method: "POST",
    data: typeof payload === "string" ? { idToken: payload } : payload,
  });
}
